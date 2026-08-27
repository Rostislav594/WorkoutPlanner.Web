using Microsoft.AspNetCore.Identity;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services.Admin;

public sealed class AdminAccessVerifier(
    CurrentUserService currentUser,
    UserManager<IdentityUser> userManager)
{
    public async Task<string> GetRequiredAdminUserIdAsync()
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.IsInRoleAsync(user, ApplicationRoles.Admin))
            throw new UnauthorizedAccessException("Administrator role is required.");

        return userId;
    }
}
