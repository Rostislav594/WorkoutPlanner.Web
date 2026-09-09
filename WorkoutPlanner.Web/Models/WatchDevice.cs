namespace WorkoutPlanner.Web.Models;

public sealed class WatchDevice
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Platform { get; set; } = "WearOS";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
    public string? AppVersion { get; set; }
    public string? DeviceModel { get; set; }
}
