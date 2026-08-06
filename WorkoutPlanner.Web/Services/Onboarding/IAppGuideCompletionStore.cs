namespace WorkoutPlanner.Web.Services.Onboarding;

public interface IAppGuideCompletionStore
{
    Task<bool> IsCompletedAsync();

    Task<AppGuideProgress?> LoadProgressAsync();

    Task<string?> LoadOutcomeAsync();

    Task SaveProgressAsync(AppGuideProgress progress);

    Task ClearProgressAsync();

    Task MarkCompletedAsync(string? outcome);

    Task ResetAsync();
}
