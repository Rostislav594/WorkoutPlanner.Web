using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace WorkoutPlanner.Web.Services.Auth;

public class CurrentUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public CurrentUserService(
        AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<string> GetRequiredUserIdAsync()
    {
        var authenticationState =
            await _authenticationStateProvider.GetAuthenticationStateAsync();

        var userId =
            authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "The current operation requires an authenticated user.");
        }

        return userId;
    }
}
