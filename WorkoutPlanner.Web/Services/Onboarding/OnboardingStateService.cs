using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Components.Onboarding;

namespace WorkoutPlanner.Web.Services.Onboarding;

public sealed class OnboardingStateService(
    AppGuideCatalog catalog,
    IAppGuideCompletionStore completionStore,
    TimeProvider timeProvider)
    : IOnboardingStateService
{
    public async Task<OnboardingState> GetStateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completed = await completionStore.IsCompletedAsync();
        var progress = await completionStore.LoadProgressAsync();
        var outcome = progress?.Outcome
            ?? await completionStore.LoadOutcomeAsync();
        return new OnboardingState(
            completed,
            progress?.StepId,
            outcome,
            progress?.UpdatedAtUtc);
    }

    public async Task<OnboardingMutationResult> SaveProgressAsync(
        string stepId,
        string? outcome,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = await GetStateAsync(cancellationToken);
        if (current.IsCompleted)
        {
            return new(
                false,
                OnboardingMutationFailure.AlreadyCompleted,
                current);
        }

        if (catalog.CreateScenario().Find(stepId) is null)
        {
            return new(false, OnboardingMutationFailure.InvalidStep, current);
        }

        if (!IsValidOutcome(outcome))
        {
            return new(false, OnboardingMutationFailure.InvalidOutcome, current);
        }

        var progress = new AppGuideProgress
        {
            StepId = stepId,
            Outcome = NormalizeOutcome(outcome),
            UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        await completionStore.SaveProgressAsync(progress);
        return new(
            true,
            OnboardingMutationFailure.None,
            new OnboardingState(
                false,
                progress.StepId,
                progress.Outcome,
                progress.UpdatedAtUtc));
    }

    public async Task<OnboardingMutationResult> CompleteAsync(
        string? outcome,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = await GetStateAsync(cancellationToken);
        if (current.IsCompleted)
        {
            return new(
                false,
                OnboardingMutationFailure.AlreadyCompleted,
                current);
        }

        if (!IsValidOutcome(outcome))
        {
            return new(false, OnboardingMutationFailure.InvalidOutcome, current);
        }

        var normalizedOutcome = NormalizeOutcome(outcome) ?? current.Outcome;
        await completionStore.MarkCompletedAsync(normalizedOutcome);
        return new(
            true,
            OnboardingMutationFailure.None,
            new OnboardingState(true, null, normalizedOutcome, null));
    }

    public async Task<OnboardingState> ResetAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await completionStore.ResetAsync();
        return new OnboardingState(false, null, null, null);
    }

    private bool IsValidOutcome(string? outcome)
    {
        var normalized = NormalizeOutcome(outcome);
        if (normalized is null)
            return true;

        return catalog.CreateScenario().Steps
            .SelectMany(x => x.Choices)
            .Select(x => x.Outcome)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Contains(normalized, StringComparer.Ordinal);
    }

    private static string? NormalizeOutcome(string? outcome) =>
        string.IsNullOrWhiteSpace(outcome) ? null : outcome.Trim();
}
