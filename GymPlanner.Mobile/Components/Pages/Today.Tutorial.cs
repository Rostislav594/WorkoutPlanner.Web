using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Tutorial;
using Microsoft.AspNetCore.Components;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Components.Pages;

/// <summary>
/// Обучение страницы «Сегодня»: ускоренная демонстрация свободной тренировки
/// настоящими элементами и действиями страницы.
/// </summary>
/// <remarks>
/// <para>
/// Сценарий не рисует копию интерфейса и не повторяет бизнес-логику: он
/// вызывает те же обработчики, что и кнопки, а прожектор лишь показывает, куда
/// «нажали». Чтобы при этом не тронуть настоящие данные, на время обучения
/// страница ходит не в API, а в <see cref="TutorialWorkoutBackend"/>, и не
/// сохраняет идущую тренировку на телефон.
/// </para>
/// <para>
/// Перед стартом состояние страницы откладывается целиком — вместе с отмеченными
/// подходами и идущими таймерами отдыха — и после обучения возвращается как было.
/// </para>
/// </remarks>
public partial class Today
{
    // Ускоряются только таймеры отдыха и анимация завершения: всё остальное идёт
    // в спокойном темпе, чтобы успеть разглядеть и запомнить каждое действие.
    // Таймер отдыха любой длины в демонстрации идёт примерно столько секунд.
    private const double TutorialRestSeconds = 3;
    // Удержание «Завершить» в демонстрации вдвое короче настоящего.
    private static readonly TimeSpan TutorialFinishHoldDuration = TimeSpan.FromMilliseconds(1700);
    // Шагов с подписью в сценарии: по ним заполняется полоска прогресса.
    private const int TutorialStepCount = 21;

    private TutorialRun? _tutorial;
    private TutorialSpotlight? _spotlight;
    private ElementReference _tutorialLayer;
    private bool _tutorialLayerAttached;

    private IWorkoutApiClient WorkoutClient =>
        (IWorkoutApiClient?)_tutorial?.Backend ?? ServerWorkoutClient;

    private IWorkoutLifecycleApiClient LifecycleClient =>
        (IWorkoutLifecycleApiClient?)_tutorial?.Backend ?? ServerLifecycleClient;

    private TimeSpan ActiveFinishHoldDuration =>
        _tutorial is null ? FinishHoldDuration : TutorialFinishHoldDuration;

    private bool CanStartTutorial =>
        _tutorial is null && !_isLoading && !_isBusy && !_isFinishHoldActive && _completedHistory is null;

    private double TutorialProgressPercent =>
        _tutorial is null ? 0 : Math.Min(100, _tutorial.Step * 100d / TutorialStepCount);

    /// <summary>
    /// Пока идёт обучение, «Назад» на телефоне завершает его, какое бы окно
    /// демонстрация ни открыла.
    /// </summary>
    private void SetBackInterceptor(Action? interceptor) =>
        BackNavigation.SetBackInterceptor(_tutorial is null ? interceptor : RequestStopTutorial);

    private void RequestStopTutorial() => _ = InvokeAsync(StopTutorialAsync);

    private async Task StartTutorialAsync()
    {
        if (!CanStartTutorial)
            return;

        // Отметки идущей тренировки сохраняются до подмены: если система
        // закроет приложение посреди демонстрации, они не потеряются.
        await SaveSessionIfChangedAsync();

        var run = new TutorialRun(new TutorialWorkoutBackend(Text), TakeSnapshot());
        CloseEditWorkoutAction();
        _selectedRestTimer = null;
        _errors.Clear();
        _workout = null;
        _isFreeWorkout = false;
        _freeDraftPlanId = null;
        _exercises.Clear();
        _exerciseDefinitions.Clear();
        _restTimers.Clear();
        _queuedRestTimers.Clear();
        _restBetweenExercisesSeconds = RestTimerDefaults.BetweenExercisesSeconds;

        _tutorial = run;
        _tutorialLayerAttached = false;
        _spotlight ??= new TutorialSpotlight(JS);
        SetBackInterceptor(null);
        StateHasChanged();

        _ = RunTutorialAsync(run);
    }

    private async Task RunTutorialAsync(TutorialRun run)
    {
        var token = run.Cancellation.Token;
        try
        {
            await PlayTutorialAsync(run, token);
            await StopTutorialAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Обучение остановили «Пропустить» или «Назад»: возврат уже сделан.
        }
    }

    private async Task PlayTutorialAsync(TutorialRun run, CancellationToken token)
    {
        // Первая подсветка до загрузки модуля прожектора просто потерялась бы.
        await run.Attached.Task.WaitAsync(token);
        run.Resync();
        await PauseAsync(700, token);

        // 1. Свободная тренировка.
        Caption(run, "Tutorial_Today_StepStart");
        await TapAsync(token, "[data-tour=\"free-start\"]", CreateFreeWorkoutAsync);

        // 2. Первое упражнение — подробно: выбор, подходы, вес и повторения.
        await FillFirstExerciseAsync(run, token, run.Backend.Definitions[0], setsCount: 3, [(60, 10), (70, 8), (75, 6)]);
        var benchId = _exercises[^1].Id;

        // 3. Ещё два упражнения: окно уже показано, поэтому рука только
        // нажимает «Добавить упражнение», и упражнение сразу на странице.
        Caption(run, "Tutorial_Today_StepMore");
        var rowId = await QuickAddExerciseAsync(token, run.Backend.Definitions[1], [(24, 12), (26, 10)]);
        var dipsId = await QuickAddExerciseAsync(token, run.Backend.Definitions[2], [(0, 12), (0, 10)]);

        // 4. Суперсет из двух последних упражнений.
        Caption(run, "Tutorial_Today_StepSuperset");
        await TapAsync(token, "[data-tour=\"superset-create\"]", () => { OpenSupersetDialog(); return Task.CompletedTask; }, after: 800);
        await TapAsync(token, $"[data-tour=\"superset-option-{rowId}\"]", () => { ToggleSupersetExercise(rowId); return Task.CompletedTask; }, after: 600);
        await TapAsync(token, $"[data-tour=\"superset-option-{dipsId}\"]", () => { ToggleSupersetExercise(dipsId); return Task.CompletedTask; }, after: 700);
        await TapAsync(token, "[data-tour=\"superset-confirm\"]", CreateSupersetAsync);

        var superset = ExerciseGroups.First(group => group.IsSuperset);
        var supersetSelector = $"[data-superset-id=\"{superset.Exercises[0].SupersetGroupId}\"]";
        Caption(run, "Tutorial_Today_StepSupersetReady");
        await ShowAsync(token, supersetSelector, 2600, $"{supersetSelector} .superset-label");

        // 5. Время отдыха между подходами первого упражнения.
        Caption(run, "Tutorial_Today_StepRest");
        await TapAsync(token, $"[data-tour-exercise=\"{benchId}\"] [data-tour=\"rest-1\"]",
            () => { OpenRestTimer(benchId, 1, RestTimerKind.BetweenSets); return Task.CompletedTask; });
        await TypeAsync(token, "[data-tour=\"rest-seconds\"]", "60", value => _restDraftSeconds = int.Parse(value));
        await TapAsync(token, "[data-tour=\"rest-done\"]", CloseRestTimerAsync);

        // 6. Выполнение: подходы первого упражнения с отдыхом между ними.
        var benchDraft = _exercises.First(x => x.Id == benchId);
        foreach (var set in benchDraft.Sets)
        {
            Caption(run, set.SetNumber == 1 ? "Tutorial_Today_StepDoSet" : "Tutorial_Today_StepNextSet");
            var row = $"[data-tour-exercise=\"{benchId}\"] [data-tour=\"set-{set.SetNumber}\"]";
            await TapAsync(token, row, () => SetCompletedAsync(benchDraft, set, new ChangeEventArgs { Value = true }),
                after: 700, finger: $"{row} .complete-control");

            if (HasFollowingSet(benchDraft, set))
            {
                Caption(run, "Tutorial_Today_StepResting");
                await WaitRestAsync(token, $"[data-tour-exercise=\"{benchId}\"] [data-tour=\"rest-{set.SetNumber}\"]",
                    GetRestTimer(benchId, set.SetNumber, RestTimerKind.BetweenSets));
            }
        }

        Caption(run, "Tutorial_Today_StepNextExercise");
        await WaitRestAsync(token, $"[data-tour-exercise=\"{benchId}\"] [data-tour=\"rest-next\"]",
            GetRestTimer(benchId, 0, RestTimerKind.BetweenExercises));

        // 7. Суперсет: круг — по подходу каждого упражнения, отдых после пары.
        superset = ExerciseGroups.First(group => group.IsSuperset);
        Caption(run, "Tutorial_Today_StepSupersetRun");
        foreach (var round in GetSupersetRoundNumbers(superset))
        {
            foreach (var exercise in superset.Exercises)
            {
                var set = exercise.Sets.First(x => x.SetNumber == round);
                var row = $"{supersetSelector} [data-tour=\"sset-{exercise.Id}-{round}\"]";
                await TapAsync(token, row, () => SetSupersetCompletedAsync(superset, set, new ChangeEventArgs { Value = true }),
                    after: 700, finger: $"{row} .complete-control");
            }

            if (HasFollowingSupersetRound(superset, round))
            {
                Caption(run, "Tutorial_Today_StepRestPair");
                await WaitRestAsync(token, $"{supersetSelector} [data-tour=\"rest-{round}\"]",
                    GetRestTimer(superset.Exercises[0].Id, round, RestTimerKind.BetweenSets));
                Caption(run, "Tutorial_Today_StepSupersetRun");
            }
        }

        // 8. Завершение: удержание кнопки, вопрос о шаблоне, итог.
        Caption(run, "Tutorial_Today_StepFinish");
        await FocusAsync(token, "[data-tour=\"finish\"]", "[data-tour=\"finish\"]");
        await PauseAsync(1200, token);
        await Spotlight.PressAsync(true);
        // Пока палец держит кнопку, в фокусе стандартная анимация завершения:
        // она рисуется поверх страницы и иначе осталась бы под вуалью.
        await FocusAsync(token, ".finish-hold-animation", "[data-tour=\"finish\"]");
        try
        {
            await BeginFinishHoldAsync();
        }
        finally
        {
            await Spotlight.PressAsync(false);
        }
        token.ThrowIfCancellationRequested();
        run.Resync();

        Caption(run, "Tutorial_Today_StepSave");
        await TapAsync(token, "[data-tour=\"template-no\"]", CompleteFreeWithoutTemplateAsync);

        Caption(run, "Tutorial_Today_StepDone", final: true);
        await ShowAsync(token, "[data-tour=\"completion\"]", 3000, "[data-tour=\"completion-done\"]");
        // В фокусе остаётся всё окно: облачко с итогом стоит над ним и не
        // закрывает текст окна, а рука нажимает «Готово».
        await TapAsync(token, "[data-tour=\"completion\"]", () => Task.CompletedTask, after: 400,
            finger: "[data-tour=\"completion-done\"]");
    }

    /// <summary>
    /// Первое упражнение — через окно нового упражнения, подробно: выбор из
    /// библиотеки, число подходов, вес и повторения набираются по цифре.
    /// </summary>
    private async Task FillFirstExerciseAsync(
        TutorialRun run,
        CancellationToken token,
        ExerciseDefinitionApiResponse definition,
        int setsCount,
        IReadOnlyList<(double Weight, int Repetitions)> sets)
    {
        Caption(run, "Tutorial_Today_StepPick");
        // Список упражнений — системное окно телефона, его не показать изнутри
        // страницы, поэтому поле только «нажимается» и получает выбор.
        await TapAsync(token, "[data-tour=\"editor-exercise\"]",
            () => { SetFreeExerciseDefinition(definition.Id.ToString()); return Task.CompletedTask; }, after: 900);

        Caption(run, "Tutorial_Today_StepSets");
        await TypeAsync(token, "[data-tour=\"editor-sets\"]", setsCount.ToString(),
            value => _freeEditorDraft?.ResizeSets(int.Parse(value)));

        Caption(run, "Tutorial_Today_StepWeightReps");
        for (var index = 0; index < sets.Count; index++)
        {
            var draftSet = _freeEditorDraft!.Sets[index];
            var (weight, repetitions) = sets[index];
            // В фокусе весь подход: облачко встаёт над ним и не закрывает «Подход N».
            var setArea = $"[data-tour=\"editor-set-{draftSet.SetNumber}\"]";
            // Первый подход — подробно, остальные быстрее.
            var fast = index > 0;
            await TypeAsync(token, $"[data-tour=\"editor-weight-{draftSet.SetNumber}\"]",
                weight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                value => draftSet.Weight = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                fast, setArea);
            await TypeAsync(token, $"[data-tour=\"editor-reps-{draftSet.SetNumber}\"]",
                repetitions.ToString(),
                value => draftSet.Repetitions = int.Parse(value),
                fast, setArea);
        }

        Caption(run, "Tutorial_Today_StepAdd");
        await TapAsync(token, "[data-tour=\"editor-save\"]", SaveFreeExerciseEdit, after: 1300);
    }

    /// <summary>
    /// Следующие упражнения: окно уже показано на первом, поэтому рука только
    /// нажимает «Добавить упражнение», и упражнение сразу появляется на странице.
    /// </summary>
    /// <returns>Идентификатор добавленного упражнения.</returns>
    private async Task<int> QuickAddExerciseAsync(
        CancellationToken token,
        ExerciseDefinitionApiResponse definition,
        IReadOnlyList<(double Weight, int Repetitions)> sets)
    {
        await TapAsync(token, "[data-tour=\"add-exercise\"]",
            () => AddExerciseWithoutEditorAsync(definition, sets), after: 0);
        var exerciseId = _exercises[^1].Id;

        // Новая карточка встаёт на место кнопки и сталкивает её вниз: страница
        // сразу докручивается к карточке, и рука показывает на неё, а не уходит
        // за кнопкой за край экрана.
        var card = $"[data-tour-exercise=\"{exerciseId}\"]";
        await ShowAsync(token, card, 1500, $"{card} .exercise-header");
        return exerciseId;
    }

    /// <summary>
    /// Черновик нового упражнения заполняется сразу и сохраняется тем же
    /// <see cref="SaveFreeExerciseEdit"/>, что и кнопка окна, — только окно не открывается.
    /// </summary>
    private async Task AddExerciseWithoutEditorAsync(
        ExerciseDefinitionApiResponse definition,
        IReadOnlyList<(double Weight, int Repetitions)> sets)
    {
        _freeEditorExerciseId = null;
        _freeEditorDraft = ExerciseDraft.CreateFree(_nextFreeExerciseId);
        _freeEditorErrors.Clear();
        _isCreatingFreeExercise = true;
        SetFreeExerciseDefinition(definition.Id.ToString());
        _freeEditorDraft.ResizeSets(sets.Count);
        for (var index = 0; index < sets.Count; index++)
        {
            _freeEditorDraft.Sets[index].Weight = sets[index].Weight;
            _freeEditorDraft.Sets[index].Repetitions = sets[index].Repetitions;
        }

        await SaveFreeExerciseEdit();
    }

    private TutorialSpotlight Spotlight => _spotlight!;

    /// <param name="final">Итог демонстрации: облачко не гаснет до её конца.</param>
    private void Caption(TutorialRun run, string key, bool final = false)
    {
        run.CaptionKey = key;
        run.CaptionFinal = final;
        run.Step++;
        StateHasChanged();
    }

    private async Task FocusAsync(CancellationToken token, string selector, string finger)
    {
        token.ThrowIfCancellationRequested();
        await Spotlight.FocusAsync(selector, finger);
    }

    /// <summary>Подсветить элемент, «нажать» на него и выполнить то, что делает нажатие.</summary>
    private async Task TapAsync(
        CancellationToken token,
        string selector,
        Func<Task> action,
        int after = 900,
        string? finger = null,
        bool fast = false)
    {
        // Рука доезжает до цели и задерживается, чтобы было видно, куда нажмут.
        await FocusAsync(token, selector, finger ?? selector);
        await PauseAsync(fast ? 800 : 1200, token);
        await Spotlight.TapAsync();
        await PauseAsync(250, token);

        token.ThrowIfCancellationRequested();
        await action();
        StateHasChanged();
        await PauseAsync(after, token);
    }

    /// <summary>
    /// Набор значения по одному символу, как с клавиатуры. В фокусе всё поле
    /// вместе с подписью (или <paramref name="area"/>), чтобы облачко встало
    /// над ним и не закрыло подпись; рука нажимает на само поле ввода.
    /// </summary>
    private async Task TypeAsync(
        CancellationToken token,
        string selector,
        string value,
        Action<string> apply,
        bool fast = false,
        string? area = null)
    {
        await TapAsync(token, area ?? selector, () => Task.CompletedTask, after: fast ? 200 : 300, finger: $"{selector} input", fast: fast);
        for (var length = 1; length <= value.Length; length++)
        {
            token.ThrowIfCancellationRequested();
            apply(value[..length]);
            StateHasChanged();
            await PauseAsync(fast ? 220 : 320, token);
        }

        await PauseAsync(fast ? 400 : 700, token);
    }

    /// <summary>
    /// Показ результата: на него не распространяется нагон отставания,
    /// готовый суперсет и итог должны быть видны полностью. Рука не исчезает,
    /// а показывает на <paramref name="finger"/>.
    /// </summary>
    private async Task ShowAsync(CancellationToken token, string selector, int duration, string finger)
    {
        await FocusAsync(token, selector, finger);
        _tutorial?.Resync();
        await PauseAsync(duration, token);
    }

    /// <summary>Таймер отдыха идёт ускоренно; ждём, пока он дойдёт до нуля.</summary>
    private async Task WaitRestAsync(CancellationToken token, string selector, RestTimerState timer)
    {
        // Рука показывает на идущий таймер и ждёт вместе с пользователем.
        await FocusAsync(token, selector, selector);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(TutorialRestSeconds * 3);
        while (!timer.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, token);
            foreach (var restTimer in _restTimers.Values.ToList())
                UpdateRestTimer(restTimer);
            StateHasChanged();
        }

        // Таймер шёл своё настоящее время: отставание до него не нагоняется.
        _tutorial?.Resync();
        await PauseAsync(900, token);
    }

    /// <summary>
    /// Пауза по общей шкале сценария. Обращения к WebView на медленном
    /// телефоне занимают заметное время; оно немного вычитается из следующих
    /// пауз, чтобы демонстрация не затягивалась, но пауза не ужимается сильнее,
    /// чем на четверть, — иначе происходящее не успеть разглядеть.
    /// </summary>
    private async Task PauseAsync(int milliseconds, CancellationToken token)
    {
        var wait = _tutorial?.NextPause(milliseconds) ?? milliseconds;
        await Task.Delay(wait, token);
    }

    private double RestTimeScale(int durationSeconds) =>
        _tutorial is null ? 1 : Math.Max(1, durationSeconds / TutorialRestSeconds);

    private async Task StopTutorialAsync()
    {
        if (_tutorial is not { } run || run.Leaving)
            return;

        run.Leaving = true;
        run.Cancellation.Cancel();
        CancelFinishHold();
        StateHasChanged();

        if (_spotlight is not null)
        {
            await _spotlight.ClearAsync();
            await _spotlight.HideFingerAsync();
        }

        // Слой гаснет, и только потом под ним возвращается настоящая страница.
        await Task.Delay(260);

        CloseTutorialDialogs();
        RestoreSnapshot(run.Snapshot);
        _tutorial = null;
        _tutorialLayerAttached = false;
        BackNavigation.SetBackInterceptor(null);
        if (_spotlight is not null)
            await _spotlight.DetachAsync();
        run.Cancellation.Dispose();
        StateHasChanged();
    }

    private void CloseTutorialDialogs()
    {
        _showFreeExerciseEditor = false;
        _freeEditorDraft = null;
        _freeEditorExerciseId = null;
        _freeEditorErrors.Clear();
        _isCreatingFreeExercise = false;
        _showSupersetDialog = false;
        _supersetSelection.Clear();
        _showFreeTemplatePrompt = false;
        _showTemplateNamePrompt = false;
        _templateErrors.Clear();
        _completedHistory = null;
        _completionMessage = string.Empty;
        _selectedRestTimer = null;
        _editActionExerciseId = null;
        _supersetActionGroup = null;
        _isBusy = false;
    }

    private async Task AttachTutorialLayerAsync()
    {
        if (_tutorial is null || _tutorialLayerAttached || _spotlight is null)
            return;

        _tutorialLayerAttached = true;
        try
        {
            await _spotlight.AttachAsync(_tutorialLayer);
        }
        finally
        {
            // Без прожектора обучение всё равно идёт, просто без подсветки.
            _tutorial?.Attached.TrySetResult();
        }
    }

    private TutorialSnapshot TakeSnapshot() => new(
        _workout,
        _isFreeWorkout,
        _freeDraftPlanId,
        _exercises.ToList(),
        _exerciseDefinitions.ToList(),
        new Dictionary<string, RestTimerState>(_restTimers),
        _queuedRestTimers.ToList(),
        _restBetweenExercisesSeconds,
        _errors.ToList());

    private void RestoreSnapshot(TutorialSnapshot snapshot)
    {
        _workout = snapshot.Workout;
        _isFreeWorkout = snapshot.IsFreeWorkout;
        _freeDraftPlanId = snapshot.FreeDraftPlanId;
        _exercises.Clear();
        _exercises.AddRange(snapshot.Exercises);
        _exerciseDefinitions.Clear();
        _exerciseDefinitions.AddRange(snapshot.ExerciseDefinitions);
        _restTimers.Clear();
        foreach (var (key, timer) in snapshot.RestTimers)
            _restTimers.Add(key, timer);
        _queuedRestTimers.Clear();
        _queuedRestTimers.AddRange(snapshot.QueuedRestTimers);
        _restBetweenExercisesSeconds = snapshot.RestBetweenExercisesSeconds;
        _errors.Clear();
        _errors.AddRange(snapshot.Errors);
        _swappedSupersetId = null;
    }

    private sealed class TutorialRun(TutorialWorkoutBackend backend, TutorialSnapshot snapshot)
    {
        public TutorialWorkoutBackend Backend { get; } = backend;
        public TutorialSnapshot Snapshot { get; } = snapshot;
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource Attached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string CaptionKey { get; set; } = "Tutorial_Today_StepStart";
        public bool CaptionFinal { get; set; }
        public int Step { get; set; }
        public bool Leaving { get; set; }

        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        // Момент, к которому сценарий должен был дойти по своим паузам.
        private long _schedule;

        public int NextPause(int milliseconds)
        {
            _schedule += milliseconds;
            var elapsed = _clock.ElapsedMilliseconds;
            var wait = (int)Math.Clamp(_schedule - elapsed, milliseconds * 3 / 4, milliseconds);
            // Большое отставание не копится: иначе все дальнейшие паузы шли бы по минимуму.
            if (elapsed + wait - _schedule > 1500)
                _schedule = elapsed + wait - 1500;
            return wait;
        }

        public void Resync() => _schedule = _clock.ElapsedMilliseconds;
    }

    private sealed record TutorialSnapshot(
        TodayWorkoutApiResponse? Workout,
        bool IsFreeWorkout,
        int? FreeDraftPlanId,
        IReadOnlyList<ExerciseDraft> Exercises,
        IReadOnlyList<ExerciseDefinitionApiResponse> ExerciseDefinitions,
        IReadOnlyDictionary<string, RestTimerState> RestTimers,
        IReadOnlyList<RestTimerState> QueuedRestTimers,
        int RestBetweenExercisesSeconds,
        IReadOnlyList<string> Errors);
}
