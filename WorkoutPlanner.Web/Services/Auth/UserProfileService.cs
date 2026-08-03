using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.Auth;

public class UserProfileService
{
    private readonly WorkoutDbContext _db;
    private readonly CurrentUserService _currentUserService;

    public UserProfileService(
        WorkoutDbContext db,
        CurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<UserProfile?> GetCurrentProfileAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        return await _db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task<bool> CurrentUserHasProfileAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        return await _db.UserProfiles
            .AnyAsync(x => x.UserId == userId);
    }
}