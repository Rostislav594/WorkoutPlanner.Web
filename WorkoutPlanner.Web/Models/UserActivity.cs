using System.ComponentModel.DataAnnotations;

namespace WorkoutPlanner.Web.Models;

public sealed class UserActivity
{
    [Key]
    public string UserId { get; set; } = string.Empty;

    public DateTime? RegisteredAtUtc { get; set; }

    public DateTime? LastSeenAtUtc { get; set; }

    [MaxLength(40)]
    public string? Platform { get; set; }

    [MaxLength(40)]
    public string? AppVersion { get; set; }

    [MaxLength(80)]
    public string? OsVersion { get; set; }

    [MaxLength(120)]
    public string? DeviceModel { get; set; }
}
