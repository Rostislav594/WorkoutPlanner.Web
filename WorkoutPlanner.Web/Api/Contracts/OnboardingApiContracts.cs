namespace WorkoutPlanner.Web.Api.Contracts;

public sealed record OnboardingStateApiResponse(
    bool IsCompleted,
    string? StepId,
    string? Outcome,
    DateTime? UpdatedAtUtc);

public sealed record SaveOnboardingProgressRequest(
    string StepId,
    string? Outcome);

public sealed record CompleteOnboardingRequest(string? Outcome);
