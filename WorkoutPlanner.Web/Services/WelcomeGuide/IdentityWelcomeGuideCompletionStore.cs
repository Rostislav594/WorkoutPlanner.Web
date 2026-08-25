using Microsoft.AspNetCore.Identity;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services.WelcomeGuide;

public sealed class IdentityWelcomeGuideCompletionStore(
    IServiceScopeFactory scopeFactory,
    CurrentUserService currentUserService) : IWelcomeGuideCompletionStore
{
    private const string LoginProvider = "GymPlanner";
    private const string CompletionTokenName = "WelcomeGuideCompletedVersion";
    private const string CurrentVersion = "1";
    private const string LegacyCompletionTokenName = "AppGuideCompletedVersion";

    public async Task<bool> IsCompletedAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var (manager, user) = await GetCurrentUserAsync(scope);
        var value = await manager.GetAuthenticationTokenAsync(user, LoginProvider, CompletionTokenName);
        if (string.Equals(value, CurrentVersion, StringComparison.Ordinal))
            return true;

        var legacyValue = await manager.GetAuthenticationTokenAsync(user, LoginProvider, LegacyCompletionTokenName);
        if (!string.Equals(legacyValue, "2", StringComparison.Ordinal))
            return false;

        await MarkCompletedAsync(manager, user);
        return true;
    }

    public async Task MarkCompletedAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var (manager, user) = await GetCurrentUserAsync(scope);
        await MarkCompletedAsync(manager, user);
    }

    public async Task ResetAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var (manager, user) = await GetCurrentUserAsync(scope);
        foreach (var token in new[] { CompletionTokenName, LegacyCompletionTokenName, "AppGuideProgressV2", "AppGuideOutcomeV2" })
            EnsureSucceeded(await manager.RemoveAuthenticationTokenAsync(user, LoginProvider, token));
    }

    private async Task<(UserManager<IdentityUser> Manager, IdentityUser User)> GetCurrentUserAsync(AsyncServiceScope scope)
    {
        var userId = await currentUserService.GetRequiredUserIdAsync();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await manager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Authenticated user was not found.");
        return (manager, user);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }

    private static async Task MarkCompletedAsync(UserManager<IdentityUser> manager, IdentityUser user)
    {
        EnsureSucceeded(await manager.SetAuthenticationTokenAsync(user, LoginProvider, CompletionTokenName, CurrentVersion));

        foreach (var legacyToken in new[] { LegacyCompletionTokenName, "AppGuideProgressV2", "AppGuideOutcomeV2" })
            EnsureSucceeded(await manager.RemoveAuthenticationTokenAsync(user, LoginProvider, legacyToken));
    }
}
