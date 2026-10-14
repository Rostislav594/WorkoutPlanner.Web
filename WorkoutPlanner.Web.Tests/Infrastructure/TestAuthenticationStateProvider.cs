using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace WorkoutPlanner.Web.Tests.Infrastructure;

internal sealed class TestAuthenticationStateProvider
    : AuthenticationStateProvider
{
    private AuthenticationState _state =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public void SetUser(string userId, params string[] roles)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                .. roles.Select(role => new Claim(ClaimTypes.Role, role))
            ],
            authenticationType: "Test");

        _state = new AuthenticationState(new ClaimsPrincipal(identity));
        NotifyAuthenticationStateChanged(Task.FromResult(_state));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(_state);
    }
}
