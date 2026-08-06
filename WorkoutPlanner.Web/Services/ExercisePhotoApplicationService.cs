using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public sealed class ExercisePhotoApplicationService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    CurrentUserService currentUser,
    IExercisePhotoService photoStorage,
    ILogger<ExercisePhotoApplicationService> logger)
    : IExercisePhotoApplicationService
{
    public async Task<ExercisePhotoMutationResult> SaveAsync(
        int exerciseId,
        PhotoUpload upload,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var exercise = await db.Exercises.FirstOrDefaultAsync(
            x => x.Id == exerciseId && x.UserId == userId,
            cancellationToken);
        if (exercise is null)
        {
            await upload.Content.DisposeAsync();
            return Failure(ExercisePhotoMutationFailure.ExerciseNotFound);
        }

        string newPhotoPath;
        try
        {
            newPhotoPath = await photoStorage.SavePhotoAsync(
                upload,
                cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Failure(ExercisePhotoMutationFailure.InvalidFile);
        }

        var previousPhotoPath = exercise.PhotoPath;
        exercise.PhotoPath = newPhotoPath;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            photoStorage.TryDeletePhoto(newPhotoPath);
            throw;
        }

        if (!photoStorage.TryDeletePhoto(previousPhotoPath))
        {
            logger.LogWarning(
                "Exercise {ExerciseId} uses its new photo, but the previous file could not be deleted.",
                exerciseId);
        }

        return Success();
    }

    public async Task<PhotoDownload?> OpenAsync(
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var photoPath = await db.Exercises
            .AsNoTracking()
            .Where(x => x.Id == exerciseId && x.UserId == userId)
            .Select(x => x.PhotoPath)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(photoPath))
            return null;

        return await photoStorage.OpenPhotoAsync(photoPath, cancellationToken);
    }

    public async Task<ExercisePhotoMutationResult> DeleteAsync(
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var exercise = await db.Exercises.FirstOrDefaultAsync(
            x => x.Id == exerciseId && x.UserId == userId,
            cancellationToken);
        if (exercise is null)
            return Failure(ExercisePhotoMutationFailure.ExerciseNotFound);

        var photoPath = exercise.PhotoPath;
        if (string.IsNullOrWhiteSpace(photoPath))
            return Success();

        exercise.PhotoPath = null;
        await db.SaveChangesAsync(cancellationToken);
        if (photoStorage.TryDeletePhoto(photoPath))
            return Success();

        exercise.PhotoPath = photoPath;
        await db.SaveChangesAsync(CancellationToken.None);
        return Failure(ExercisePhotoMutationFailure.StorageFailure);
    }

    private static ExercisePhotoMutationResult Success() =>
        new(true, ExercisePhotoMutationFailure.None);

    private static ExercisePhotoMutationResult Failure(
        ExercisePhotoMutationFailure failure) =>
        new(false, failure);
}
