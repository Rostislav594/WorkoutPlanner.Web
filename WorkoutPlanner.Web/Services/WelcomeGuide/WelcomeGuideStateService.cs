using WorkoutPlanner.Web.Application.Abstractions;

namespace WorkoutPlanner.Web.Services.WelcomeGuide;

public sealed class WelcomeGuideStateService(IWelcomeGuideCompletionStore completionStore) : IWelcomeGuideStateService
{
    public Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return completionStore.IsCompletedAsync();
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return completionStore.MarkCompletedAsync();
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return completionStore.ResetAsync();
    }
}
