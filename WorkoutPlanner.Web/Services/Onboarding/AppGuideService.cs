using WorkoutPlanner.Web.Components.Onboarding;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services.Onboarding;

public sealed class AppGuideService : IOnboardingService
{
    private readonly AppGuideCatalog _catalog;
    private readonly IAppGuideCompletionStore _completionStore;
    private readonly IProfileService _userProfileService;
    private readonly AppGuidePracticeService _practiceService;
    private readonly Stack<string> _backStack = new();
    private AppGuideScenario? _scenario;
    private string? _currentStepId;
    private string? _outcome;
    private bool _isInitializing;
    private bool _hasCheckedAutoStart;

    public AppGuideService(
        AppGuideCatalog catalog,
        IAppGuideCompletionStore completionStore,
        IProfileService userProfileService,
        AppGuidePracticeService practiceService)
    {
        _catalog = catalog;
        _completionStore = completionStore;
        _userProfileService = userProfileService;
        _practiceService = practiceService;
    }

    public event Action? StateChanged;

    public GuideStep? CurrentStep => _scenario?.Find(_currentStepId);
    public bool IsActive { get; private set; }
    public bool IsBusy { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int Revision { get; private set; }
    public bool CanGoBack => CurrentStep?.AllowBack == true && _backStack.Count > 0;

    public int SectionStepNumber
    {
        get
        {
            var step = CurrentStep;
            if (step is null || _scenario is null)
                return 0;

            var sectionSteps = _scenario.Steps.Where(x => x.Section == step.Section).ToList();
            return Math.Max(1, sectionSteps.FindIndex(x => x.Id == step.Id) + 1);
        }
    }

    public int SectionStepCount => CurrentStep is { } step && _scenario is not null
        ? _scenario.Steps.Count(x => x.Section == step.Section)
        : 0;

    public async Task TryStartAutomaticallyAsync()
    {
        if (_hasCheckedAutoStart || _isInitializing || IsActive)
            return;

        _isInitializing = true;
        try
        {
            if (!await _userProfileService.CurrentUserHasProfileAsync())
                return;

            if (await _completionStore.IsCompletedAsync())
            {
                _hasCheckedAutoStart = true;
                return;
            }

            _scenario = _catalog.CreateScenario();
            var progress = await _completionStore.LoadProgressAsync();
            var resumeStep = _scenario.Find(progress?.StepId);
            _outcome = progress?.Outcome;

            if (resumeStep?.Section == GuideSection.Practice)
                await _practiceService.EnsureTutorialPlanAsync();

            Activate(resumeStep?.Id ?? AppGuideCatalog.FirstStepId);
            _hasCheckedAutoStart = true;
        }
        finally
        {
            _isInitializing = false;
        }
    }

    public Task StartGuideAsync() => StartFreshAsync(AppGuideCatalog.FirstStepId, null);

    public Task StartNavigationTourAsync() => StartFreshAsync(AppGuideCatalog.FirstStepId, null);

    public async Task StartFirstWorkoutTutorialAsync()
    {
        if (_isInitializing || IsBusy)
            return;

        IsBusy = true;
        NotifyChanged(false);
        try
        {
            await _practiceService.EnsureTutorialPlanAsync();
            await StartFreshCoreAsync(AppGuideCatalog.PracticeFirstStepId, "guided");
        }
        finally
        {
            IsBusy = false;
            NotifyChanged(false);
        }
    }

    public async Task NextAsync()
    {
        var step = CurrentStep;
        if (!IsActive || IsBusy || step is null || step.Choices.Count > 0 || step.ExpectedActionId is not null)
            return;

        if (step.IsFinal)
        {
            await CompleteAsync();
            return;
        }

        if (step.NextStepId is not null)
            await RunBusyAsync(() => TransitionAsync(step.NextStepId, pushCurrent: true));
    }

    public async Task PreviousAsync()
    {
        if (!IsActive || IsBusy || !CanGoBack)
            return;

        var previousStepId = _backStack.Peek();
        await RunBusyAsync(async () =>
        {
            _backStack.Pop();
            await TransitionAsync(previousStepId, pushCurrent: false);
        });
    }

    public async Task SelectChoiceAsync(string choiceId)
    {
        var step = CurrentStep;
        var choice = step?.Choices.FirstOrDefault(x => x.Id == choiceId);
        if (!IsActive || IsBusy || choice is null)
            return;

        await RunBusyAsync(async () =>
        {
            if (choice.OnSelectedAsync is not null)
                await choice.OnSelectedAsync(CancellationToken.None);

            if (!string.IsNullOrWhiteSpace(choice.Outcome))
                _outcome = choice.Outcome;

            await TransitionAsync(choice.NextStepId, pushCurrent: true);
        });
    }

    public async Task NotifyActionAsync(string actionId)
    {
        var step = CurrentStep;
        if (!IsActive || IsBusy || step is null ||
            !string.Equals(step.ExpectedActionId, actionId, StringComparison.Ordinal) ||
            !step.AutoAdvanceOnAction || step.NextStepId is null)
            return;

        await RunBusyAsync(() => TransitionAsync(step.NextStepId, pushCurrent: true));
    }

    public async Task HandleUnavailableStepAsync()
    {
        var step = CurrentStep;
        if (step is null)
            return;

        if (step.IsOptional && step.NextStepId is not null)
        {
            await TransitionAsync(step.NextStepId, pushCurrent: true);
            return;
        }

        ErrorMessage = "Не удалось найти нужный элемент на странице. Обнови страницу или попробуй ещё раз.";
        NotifyChanged(false);
    }

    public void RetryCurrentStep()
    {
        if (!IsActive)
            return;

        ErrorMessage = null;
        NotifyChanged();
    }

    public async Task PauseAsync()
    {
        if (CurrentStep is not null)
            await SaveProgressAsync();

        IsActive = false;
        ErrorMessage = null;
        NotifyChanged();
    }

    private async Task StartFreshAsync(string stepId, string? outcome)
    {
        if (_isInitializing || IsBusy)
            return;

        _isInitializing = true;
        try
        {
            await StartFreshCoreAsync(stepId, outcome);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task StartFreshCoreAsync(string stepId, string? outcome)
    {
        _scenario = _catalog.CreateScenario();
        _outcome = outcome;
        _backStack.Clear();
        await _completionStore.ClearProgressAsync();
        Activate(stepId);
        await SaveProgressAsync();
    }

    private void Activate(string stepId)
    {
        _scenario ??= _catalog.CreateScenario();
        if (_scenario.Find(stepId) is null)
            throw new InvalidOperationException($"Unknown app guide step '{stepId}'.");

        _currentStepId = stepId;
        _backStack.Clear();
        ErrorMessage = null;
        IsActive = true;
        NotifyChanged();
    }

    private async Task TransitionAsync(string stepId, bool pushCurrent)
    {
        if (_scenario?.Find(stepId) is null)
            throw new InvalidOperationException($"Unknown app guide step '{stepId}'.");

        if (pushCurrent && _currentStepId is not null)
            _backStack.Push(_currentStepId);

        _currentStepId = stepId;
        ErrorMessage = null;
        await SaveProgressAsync();
        NotifyChanged();
    }

    private Task SaveProgressAsync()
    {
        var step = CurrentStep;
        if (step is null)
            return Task.CompletedTask;

        return _completionStore.SaveProgressAsync(new AppGuideProgress
        {
            StepId = step.StableResumeStepId ?? step.Id,
            Outcome = _outcome,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task CompleteAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _completionStore.MarkCompletedAsync(_outcome);
            IsActive = false;
            _currentStepId = null;
            _backStack.Clear();
            NotifyChanged();
        });
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        IsBusy = true;
        NotifyChanged(false);
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
            NotifyChanged(false);
        }
    }

    private void NotifyChanged(bool incrementRevision = true)
    {
        if (incrementRevision)
            Revision++;
        StateChanged?.Invoke();
    }
}
