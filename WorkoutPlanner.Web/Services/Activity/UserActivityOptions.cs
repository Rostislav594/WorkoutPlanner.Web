namespace WorkoutPlanner.Web.Services.Activity;

public sealed class UserActivityOptions
{
    public const string SectionName = "UserActivity";

    public TimeSpan WriteThrottle { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan ActiveWindow { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan DormantWindow { get; set; } = TimeSpan.FromDays(30);
}
