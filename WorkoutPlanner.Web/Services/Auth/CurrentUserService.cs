using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace WorkoutPlanner.Web.Services.Auth;

public class CurrentUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        AuthenticationStateProvider authenticationStateProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> GetRequiredUserIdAsync()
    {
        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            var authenticationState =
                await _authenticationStateProvider.GetAuthenticationStateAsync();
            userId = authenticationState.User
                .FindFirstValue(ClaimTypes.NameIdentifier);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "The current operation requires an authenticated user.");
        }

        return userId;
    }
}
