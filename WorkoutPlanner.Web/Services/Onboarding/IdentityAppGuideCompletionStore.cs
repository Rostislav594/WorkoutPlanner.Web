using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services.Onboarding;

public sealed class IdentityAppGuideCompletionStore : IAppGuideCompletionStore
{
    private const string LoginProvider = "GymPlanner";
    private const string CompletionTokenName = "AppGuideCompletedVersion";
    private const string ProgressTokenName = "AppGuideProgressV2";
    private const string OutcomeTokenName = "AppGuideOutcomeV2";
    private const string CurrentVersion = "2";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CurrentUserService _currentUserService;

    public IdentityAppGuideCompletionStore(
        IServiceScopeFactory scopeFactory,
        CurrentUserService currentUserService)
    {
        _scopeFactory = scopeFactory;
        _currentUserService = currentUserService;
    }

    public async Task<bool> IsCompletedAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var (userManager, user) = await GetCurrentUserAsync(scope);
        var value = await userManager.GetAuthenticationTokenAsync(
            user,
            LoginProvider,
            CompletionTokenName);

        return string.Equals(value, CurrentVersion, StringComparison.Ordinal);
    }

    public async Task<AppGuideProgress?> LoadProgressAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var (userManager, user) = await GetCurrentUserAsync(scope);
        var json = await userManager.GetAuthenticationTokenAsync(
            user,
            LoginProvider,
            ProgressTokenName);

        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AppGuideProgress>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<string?> LoadOutcomeAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var (userManager, user) = await GetCurrentUserAsync(scope);
        return await userManager.GetAuthenticationTokenAsync(
            user,
            LoginProvider,
            OutcomeTokenName);
    }

    public async Task SaveProgressAsync(AppGuideProgress progress)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var (userManager, user) = await GetCurrentUserAsync(scope);
        var result = await userManager.SetAuthenticationTokenAsync(
            user,
            LoginProvider,
            ProgressTokenName,
            JsonSerializer.Serialize(progress));

        EnsureSucceeded(result);
    }

    public async Task ClearProgressAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var (userManager, user) = await GetCurrentUserAsync(scope);
        var result = await userManager.RemoveAuthenticationTokenAsync(
            user,
            LoginProvider,
            ProgressTokenName);

        EnsureSucceeded(result);
    }

    public async Task MarkCompletedAsync(string? outcome)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var (userManager, user) = await GetCurrentUserAsync(scope);
        IdentityResult result;

        if (!string.IsNullOrWhiteSpace(outcome))
        {
            result = await userManager.SetAuthenticationTokenAsync(
                user,
                LoginProvider,
                OutcomeTokenName,
                outcome);
            EnsureSucceeded(result);
        }

        result = await userManager.RemoveAuthenticationTokenAsync(
            user,
            LoginProvider,
            ProgressTokenName);
        EnsureSucceeded(result);

        result = await userManager.SetAuthenticationTokenAsync(
            user,
            LoginProvider,
            CompletionTokenName,
            CurrentVersion);
        EnsureSucceeded(result);
    }

    public async Task ResetAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var (userManager, user) = await GetCurrentUserAsync(scope);

        foreach (var tokenName in new[]
                 {
                     CompletionTokenName,
                     ProgressTokenName,
                     OutcomeTokenName
                 })
        {
            var result = await userManager.RemoveAuthenticationTokenAsync(
                user,
                LoginProvider,
                tokenName);
            EnsureSucceeded(result);
        }
    }

    private async Task<(UserManager<IdentityUser> Manager, IdentityUser User)> GetCurrentUserAsync(
        AsyncServiceScope scope)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await manager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Authenticated user was not found.");
        return (manager, user);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(" ", result.Errors.Select(x => x.Description)));
        }
    }
}
