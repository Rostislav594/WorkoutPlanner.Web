using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace GymPlanner.Mobile.Authentication;

public sealed class MobileAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly MobileAuthenticationService _authentication;

    public MobileAuthenticationStateProvider(
        MobileAuthenticationService authentication)
    {
        _authentication = authentication;
        _authentication.AuthenticationChanged += NotifyStateChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await _authentication.InitializeAsync();
        var email = _authentication.CurrentEmail;
        if (string.IsNullOrWhiteSpace(email))
            return Anonymous;

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email)
            ],
            authenticationType: "Bearer");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private void NotifyStateChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
