using WorkoutPlanner.Web.Components.Onboarding;

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
