namespace WorkoutPlanner.Web.Services.WelcomeGuide;

public interface IWelcomeGuideCompletionStore
{
    Task<bool> IsCompletedAsync();
    Task MarkCompletedAsync();
    Task ResetAsync();
}
