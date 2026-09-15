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

public sealed record RenameWatchDeviceRequest(string DisplayName);

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

// --- Сопряжение подтверждением на телефоне ---
// Часы создают заявку, открывают ссылку на телефоне, пользователь подтверждает
// одним нажатием, часы забирают токены. Ручной ввод кода остаётся запасным путём.

public sealed record StartWatchPairingRequest(
    string DeviceId,
    string DisplayName,
    string? DeviceModel,
    string? AppVersion);

public sealed record StartWatchPairingResponse(
    string RequestId,
    string PollToken,
    string ApproveUrl,
    DateTime ExpiresAtUtc);

/// <summary>Что показать на экране подтверждения телефона.</summary>
public sealed record WatchPairingRequestDetailsResponse(
    string RequestId,
    string DisplayName,
    string? DeviceModel,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    string Status);

public sealed record WatchPairingStatusRequest(
    string RequestId,
    string PollToken);

/// <summary>
/// Статусы: pending, approved, rejected, expired.
/// Токены приезжают ровно один раз — вместе с первым approved.
/// </summary>
public sealed record WatchPairingStatusResponse(
    string Status,
    WatchTokenResponse? Tokens);

public static class WatchPairingStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Expired = "expired";
}
