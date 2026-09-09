using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.WearOs;

public sealed class WatchTokenService(
    SignInManager<IdentityUser> signInManager,
    IDataProtectionProvider dataProtectionProvider,
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    IOptions<WatchPairingOptions> watchOptions,
    TimeProvider timeProvider)
{
    public const string DeviceIdClaimType = "gymplanner:watch_device";
    public const string IdentityTypeClaimType = "gymplanner:identity_type";
    public const string WatchIdentityType = "watch";

    private readonly IDataProtector _refreshProtector = dataProtectionProvider
        .CreateProtector("GymPlanner.WearOS.RefreshToken.v1");

    public async Task<IssuedWatchTokens> IssueAsync(
        IdentityUser user,
        WatchDevice device)
    {
        var now = timeProvider.GetUtcNow();
        var options = watchOptions.Value;
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        if (principal.Identity is not ClaimsIdentity identity)
            throw new InvalidOperationException("Watch identity could not be created.");

        identity.AddClaim(new Claim(DeviceIdClaimType, device.Id.ToString("D")));
        identity.AddClaim(new Claim(IdentityTypeClaimType, WatchIdentityType));

        var expiresAt = now.Add(options.AccessTokenLifetime);
        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties
            {
                IssuedUtc = now,
                ExpiresUtc = expiresAt
            },
            IdentityConstants.BearerScheme);
        var accessToken = bearerOptions
            .Get(IdentityConstants.BearerScheme)
            .BearerTokenProtector
            .Protect(ticket);

        var refreshExpiresAt = now.Add(options.RefreshTokenLifetime);
        var refreshPayload = new WatchRefreshTokenPayload(
            device.Id,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            user.SecurityStamp ?? string.Empty,
            refreshExpiresAt.UtcDateTime);
        var refreshToken = _refreshProtector.Protect(
            JsonSerializer.Serialize(refreshPayload));

        return new IssuedWatchTokens(
            new WatchTokenResponse(
                "Bearer",
                accessToken,
                Math.Max(1, (long)options.AccessTokenLifetime.TotalSeconds),
                refreshToken),
            Hash(refreshToken),
            refreshExpiresAt.UtcDateTime);
    }

    public bool TryReadRefreshToken(
        string token,
        out WatchRefreshTokenPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            payload = JsonSerializer.Deserialize<WatchRefreshTokenPayload>(
                _refreshProtector.Unprotect(token));
            return payload is not null;
        }
        catch (Exception exception) when (
            exception is CryptographicException or JsonException)
        {
            return false;
        }
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static bool HashMatches(string token, string expectedHash)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed record IssuedWatchTokens(
    WatchTokenResponse Response,
    string RefreshTokenHash,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record WatchRefreshTokenPayload(
    Guid DeviceId,
    string Nonce,
    string SecurityStamp,
    DateTime ExpiresAtUtc);
