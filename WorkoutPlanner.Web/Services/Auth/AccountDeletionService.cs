using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.Auth;

public sealed class AccountDeletionService
{
    private readonly WorkoutDbContext _db;
    private readonly CurrentUserService _currentUserService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        WorkoutDbContext db,
        CurrentUserService currentUserService,
        UserManager<IdentityUser> userManager,
        IWebHostEnvironment environment,
        ILogger<AccountDeletionService> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _environment = environment;
        _logger = logger;
    }

    public async Task<IdentityResult> DeleteCurrentAccountAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return IdentityResult.Failed(
                new IdentityError
                {
                    Code = "UserNotFound",
                    Description = "The authenticated user no longer exists."
                });
        }

        var trainingPlanIds = await _db.TrainingPlans
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var photoPaths = await _db.Exercises
            .AsNoTracking()
            .Where(x =>
                (x.UserId == userId || trainingPlanIds.Contains(x.TrainingPlanId)) &&
                x.PhotoPath != null)
            .Select(x => x.PhotoPath!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var historyDetails = await _db.WorkoutHistory
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Details)
            .ToListAsync(cancellationToken);

        photoPaths.AddRange(GetHistoryPhotoPaths(historyDetails));
        photoPaths = photoPaths.Distinct(StringComparer.Ordinal).ToList();

        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        var stagedFiles = new List<StagedFile>();

        try
        {
            stagedFiles = StagePhotos(photoPaths, cancellationToken);
            _db.ChangeTracker.Clear();

            await _db.WorkoutDays
                .Where(x =>
                    x.UserId == userId ||
                    trainingPlanIds.Contains(x.TrainingPlanId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.WorkoutHistory
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _db.ProgressSnapshots
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _db.ExerciseProgressSnapshots
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _db.TrainingSessions
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _db.Exercises
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _db.TrainingPlans
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _db.UserProfiles
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            var identityResult = await _userManager.DeleteAsync(user);

            if (!identityResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                RestorePhotos(stagedFiles);
                return identityResult;
            }

            await transaction.CommitAsync(cancellationToken);
            DeleteStagedPhotos(stagedFiles);

            return IdentityResult.Success;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            RestorePhotos(stagedFiles);
            throw;
        }
    }

    private List<StagedFile> StagePhotos(
        IEnumerable<string> photoPaths,
        CancellationToken cancellationToken)
    {
        var stagedFiles = new List<StagedFile>();
        var stagingDirectory = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "AccountDeletionTrash",
            Guid.NewGuid().ToString("N"));

        try
        {
            foreach (var photoPath in photoPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(photoPath);

                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                StageIfExists(
                    Path.Combine(
                        _environment.ContentRootPath,
                        "App_Data",
                        "WorkoutImages",
                        fileName),
                    stagingDirectory,
                    stagedFiles);

                StageIfExists(
                    Path.Combine(
                        _environment.WebRootPath,
                        "WorkoutImages",
                        fileName),
                    stagingDirectory,
                    stagedFiles);
            }

            return stagedFiles;
        }
        catch
        {
            RestorePhotos(stagedFiles);
            throw;
        }
    }

    private IEnumerable<string> GetHistoryPhotoPaths(
        IEnumerable<string> historyDetails)
    {
        foreach (var details in historyDetails)
        {
            if (string.IsNullOrWhiteSpace(details))
                continue;

            WorkoutHistoryDetails? history;

            try
            {
                history = JsonSerializer.Deserialize<WorkoutHistoryDetails>(details);
            }
            catch (JsonException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Skipped legacy workout history details while collecting account photos.");
                continue;
            }

            if (history is null)
                continue;

            foreach (var photoPath in history.Exercises
                         .SelectMany(x => x.Photos)
                         .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                yield return photoPath;
            }
        }
    }

    private static void StageIfExists(
        string originalPath,
        string stagingDirectory,
        ICollection<StagedFile> stagedFiles)
    {
        if (!File.Exists(originalPath))
            return;

        Directory.CreateDirectory(stagingDirectory);

        var stagedPath = Path.Combine(
            stagingDirectory,
            $"{Guid.NewGuid():N}{Path.GetExtension(originalPath)}");

        File.Move(originalPath, stagedPath);
        stagedFiles.Add(new StagedFile(originalPath, stagedPath));
    }

    private void RestorePhotos(IReadOnlyList<StagedFile> stagedFiles)
    {
        foreach (var stagedFile in stagedFiles.Reverse())
        {
            try
            {
                if (!File.Exists(stagedFile.StagedPath))
                    continue;

                Directory.CreateDirectory(
                    Path.GetDirectoryName(stagedFile.OriginalPath)!);
                File.Move(stagedFile.StagedPath, stagedFile.OriginalPath);
            }
            catch (Exception exception)
            {
                _logger.LogCritical(
                    exception,
                    "Failed to restore account photo {PhotoPath} after account deletion rollback.",
                    stagedFile.OriginalPath);
            }
        }

        TryDeleteStagingDirectory(stagedFiles);
    }

    private void DeleteStagedPhotos(IReadOnlyList<StagedFile> stagedFiles)
    {
        foreach (var stagedFile in stagedFiles)
        {
            try
            {
                File.Delete(stagedFile.StagedPath);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not purge staged account photo {PhotoPath}.",
                    stagedFile.StagedPath);
            }
        }

        TryDeleteStagingDirectory(stagedFiles);
    }

    private void TryDeleteStagingDirectory(IReadOnlyList<StagedFile> stagedFiles)
    {
        var stagingDirectory = stagedFiles
            .Select(x => Path.GetDirectoryName(x.StagedPath))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (stagingDirectory is null || !Directory.Exists(stagingDirectory))
            return;

        try
        {
            Directory.Delete(stagingDirectory, recursive: false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not remove account photo staging directory {Directory}.",
                stagingDirectory);
        }
    }

    private sealed record StagedFile(
        string OriginalPath,
        string StagedPath);
}
