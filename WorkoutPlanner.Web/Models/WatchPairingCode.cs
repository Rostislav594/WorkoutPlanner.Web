namespace WorkoutPlanner.Web.Models;

public sealed class WatchPairingCode
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public int AttemptCount { get; set; }
}
