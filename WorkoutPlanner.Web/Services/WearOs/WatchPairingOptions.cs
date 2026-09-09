namespace WorkoutPlanner.Web.Services.WearOs;

public sealed class WatchPairingOptions
{
    public const string SectionName = "WatchPairing";

    public TimeSpan PairingCodeLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);
    public int MaximumPairingAttempts { get; set; } = 5;
}
