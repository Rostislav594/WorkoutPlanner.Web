namespace WorkoutPlanner.Web.Models;

public sealed class PushDeviceRegistration
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string InstallationId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string PushToken { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public bool IsActive { get; set; }
}
