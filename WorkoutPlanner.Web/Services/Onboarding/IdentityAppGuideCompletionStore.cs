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

    private readonly UserManager<IdentityUser> _userManager;
    private readonly CurrentUserService _currentUserService;

    public IdentityAppGuideCompletionStore(
        UserManager<IdentityUser> userManager,
        CurrentUserService currentUserService)
    {
        _userManager = userManager;
        _currentUserService = currentUserService;
    }

    public async Task<bool> IsCompletedAsync()
    {
        var user = await GetCurrentUserAsync();
        var value = await _userManager.GetAuthenticationTokenAsync(
            user,
            LoginProvider,
            CompletionTokenName);

        return string.Equals(value, CurrentVersion, StringComparison.Ordinal);
    }

    public async Task<AppGuideProgress?> LoadProgressAsync()
    {
        var user = await GetCurrentUserAsync();
        var json = await _userManager.GetAuthenticationTokenAsync(
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

    public async Task SaveProgressAsync(AppGuideProgress progress)
    {
        var user = await GetCurrentUserAsync();
        var result = await _userManager.SetAuthenticationTokenAsync(
            user,
            LoginProvider,
            ProgressTokenName,
            JsonSerializer.Serialize(progress));

        EnsureSucceeded(result);
    }

    public async Task ClearProgressAsync()
    {
        var user = await GetCurrentUserAsync();
        var result = await _userManager.RemoveAuthenticationTokenAsync(
            user,
            LoginProvider,
            ProgressTokenName);

        EnsureSucceeded(result);
    }

    public async Task MarkCompletedAsync(string? outcome)
    {
        var user = await GetCurrentUserAsync();
        IdentityResult result;

        if (!string.IsNullOrWhiteSpace(outcome))
        {
            result = await _userManager.SetAuthenticationTokenAsync(
                user,
                LoginProvider,
                OutcomeTokenName,
                outcome);
            EnsureSucceeded(result);
        }

        result = await _userManager.RemoveAuthenticationTokenAsync(
            user,
            LoginProvider,
            ProgressTokenName);
        EnsureSucceeded(result);

        result = await _userManager.SetAuthenticationTokenAsync(
            user,
            LoginProvider,
            CompletionTokenName,
            CurrentVersion);
        EnsureSucceeded(result);
    }

    private async Task<IdentityUser> GetCurrentUserAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        return await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Authenticated user was not found.");
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
