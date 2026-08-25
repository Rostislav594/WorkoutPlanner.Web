using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;

namespace WorkoutPlanner.Web.Services.Auth;

public sealed class UserProfileService : IProfileService
{
    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;
    private readonly CurrentUserService _currentUser;

    public UserProfileService(IDbContextFactory<WorkoutDbContext> dbFactory, CurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
    }

    public async Task<UserProfile?> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
            return null;
        var email = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(cancellationToken);
        return new UserProfile
        {
            Email = email ?? string.Empty,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            BirthDate = profile.BirthDate,
            Gender = profile.Gender,
            RestBetweenSetsSeconds = profile.RestBetweenSetsSeconds,
            RestBetweenExercisesSeconds = profile.RestBetweenExercisesSeconds
        };
    }

    public async Task<bool> CurrentUserHasProfileAsync(CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.UserProfiles.AsNoTracking()
            .AnyAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task<bool> CreateCurrentProfileAsync(ProfileUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.UserProfiles.AnyAsync(x => x.UserId == userId, cancellationToken))
            return false;
        db.UserProfiles.Add(new Models.UserProfile
        {
            UserId = userId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateCurrentProfileAsync(ProfileUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
            return false;
        profile.FirstName = request.FirstName.Trim();
        profile.LastName = request.LastName.Trim();
        profile.BirthDate = request.BirthDate;
        profile.Gender = request.Gender;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateRestTimerSettingsAsync(
        RestTimerSettingsUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
            return false;

        profile.RestBetweenSetsSeconds = request.RestBetweenSetsSeconds;
        profile.RestBetweenExercisesSeconds = request.RestBetweenExercisesSeconds;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
