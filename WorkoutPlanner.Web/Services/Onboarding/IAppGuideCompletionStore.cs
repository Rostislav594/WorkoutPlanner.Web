namespace WorkoutPlanner.Web.Services.Onboarding;

public interface IAppGuideCompletionStore
{
    Task<bool> IsCompletedAsync();

    Task<AppGuideProgress?> LoadProgressAsync();

    Task SaveProgressAsync(AppGuideProgress progress);

    Task ClearProgressAsync();

    Task MarkCompletedAsync(string? outcome);
}
