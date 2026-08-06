namespace WorkoutPlanner.Web.Application.Contracts;

public sealed record OnboardingState(
    bool IsCompleted,
    string? StepId,
    string? Outcome,
    DateTime? UpdatedAtUtc);

public enum OnboardingMutationFailure
{
    None,
    InvalidStep,
    InvalidOutcome,
    AlreadyCompleted
}

public sealed record OnboardingMutationResult(
    bool Succeeded,
    OnboardingMutationFailure Failure,
    OnboardingState State);
