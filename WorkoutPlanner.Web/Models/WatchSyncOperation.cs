namespace WorkoutPlanner.Web.Models;

public sealed class WatchSyncOperation
{
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public Guid OperationId { get; set; }
    public Guid WatchDeviceId { get; set; }
    public WatchDevice? WatchDevice { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string ResultJson { get; set; } = string.Empty;
}
