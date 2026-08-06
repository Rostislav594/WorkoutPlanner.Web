namespace GymPlanner.Mobile.Authentication;

public sealed record MobileTokenSet(
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc);
