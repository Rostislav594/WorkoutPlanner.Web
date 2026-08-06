namespace WorkoutPlanner.Web.Models;

public sealed class MobileSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastRefreshedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
