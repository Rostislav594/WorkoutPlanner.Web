using WorkoutPlanner.Web.Components.Onboarding;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IOnboardingService
{
    event Action? StateChanged;
    GuideStep? CurrentStep { get; }
    bool IsActive { get; }
    bool IsBusy { get; }
    string? ErrorMessage { get; }
    int Revision { get; }
    bool CanGoBack { get; }
    int SectionStepNumber { get; }
    int SectionStepCount { get; }
    Task TryStartAutomaticallyAsync();
    Task StartGuideAsync();
    Task StartNavigationTourAsync();
    Task StartFirstWorkoutTutorialAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task SelectChoiceAsync(string choiceId);
    Task NotifyActionAsync(string actionId);
    Task HandleUnavailableStepAsync();
    void RetryCurrentStep();
    Task PauseAsync();
}

public interface IOnboardingStateService
{
    Task<OnboardingState> GetStateAsync(
        CancellationToken cancellationToken = default);
    Task<OnboardingMutationResult> SaveProgressAsync(
        string stepId,
        string? outcome,
        CancellationToken cancellationToken = default);
    Task<OnboardingMutationResult> CompleteAsync(
        string? outcome,
        CancellationToken cancellationToken = default);
    Task<OnboardingState> ResetAsync(
        CancellationToken cancellationToken = default);
}
