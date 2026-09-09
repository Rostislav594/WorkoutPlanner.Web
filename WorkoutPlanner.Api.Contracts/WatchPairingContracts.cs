namespace WorkoutPlanner.Api.Contracts;

public sealed record CreateWatchPairingCodeResponse(
    string Code,
    DateTime ExpiresAtUtc);

public sealed record PairWatchRequest(
    string Code,
    string DeviceId,
    string DisplayName,
    string? DeviceModel,
    string? AppVersion);

public sealed record RefreshWatchTokenRequest(string RefreshToken);

public sealed record WatchTokenResponse(
    string TokenType,
    string AccessToken,
    long ExpiresIn,
    string RefreshToken);

public sealed record WatchDeviceResponse(
    Guid Id,
    string DeviceId,
    string DisplayName,
    string Platform,
    DateTime CreatedAtUtc,
    DateTime? LastSeenAtUtc,
    bool IsRevoked,
    string? AppVersion,
    string? DeviceModel);
