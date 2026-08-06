namespace WorkoutPlanner.Web.Services.Onboarding;

public sealed class AppGuideProgress
{
    public string StepId { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
