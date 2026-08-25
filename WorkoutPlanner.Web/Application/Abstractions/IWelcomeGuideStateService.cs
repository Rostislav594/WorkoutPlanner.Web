namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IWelcomeGuideStateService
{
    Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default);
    Task CompleteAsync(CancellationToken cancellationToken = default);
    Task ResetAsync(CancellationToken cancellationToken = default);
}
