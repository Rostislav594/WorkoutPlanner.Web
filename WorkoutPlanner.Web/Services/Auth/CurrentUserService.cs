using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace WorkoutPlanner.Web.Services.Auth;

public class CurrentUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly WorkoutPlanner.Web.Api.Security.MobileSessionService? _sessions;

    public CurrentUserService(
        AuthenticationStateProvider authenticationStateProvider,
        IHttpContextAccessor httpContextAccessor,
        WorkoutPlanner.Web.Api.Security.MobileSessionService? sessions = null)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _httpContextAccessor = httpContextAccessor;
        _sessions = sessions;
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

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            principal = (await _authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (_sessions is not null &&
            WorkoutPlanner.Web.Api.Security.MobileSessionService.TryGetSessionId(principal, out _) &&
            !await _sessions.IsActiveAsync(principal, userId, CancellationToken.None))
            throw new UnauthorizedAccessException("The account session has ended.");
        return userId;
    }
}
