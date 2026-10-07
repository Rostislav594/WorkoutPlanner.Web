using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Tutorial;
using Microsoft.AspNetCore.Components;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Components.Pages;

/// <summary>
/// Обучение, которое открывает «?» на «Сегодня». Слой, выбор проходки и
/// проходку по шаблону ведёт <see cref="TutorialHost"/>; здесь — свободная
/// тренировка настоящими элементами и действиями страницы.
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
/// Если проходка по шаблону увела на другие страницы, «Сегодня» создаётся
/// заново и после обучения просто загружает настоящие данные.
/// </para>
/// </remarks>
public partial class Today
{
    // Таймеры отдыха и анимация завершения ускорены; набор значений и эпизод
    // выполнения идут бойко, остальные нажатия — в спокойном темпе.
    // Таймер отдыха любой длины в демонстрации идёт примерно столько секунд.
    private const double TutorialRestSeconds = 2.5;
    // Удержание «Завершить» в демонстрации вдвое короче настоящего.
    private static readonly TimeSpan TutorialFinishHoldDuration = TimeSpan.FromMilliseconds(1700);
    // Настоящее состояние страницы, отложенное на время обучения.
    private TutorialSnapshot? _tutorialSnapshot;

    private IWorkoutApiClient WorkoutClient =>
        (IWorkoutApiClient?)Tutorial.Backend ?? ServerWorkoutClient;

    private IWorkoutLifecycleApiClient LifecycleClient =>
        (IWorkoutLifecycleApiClient?)Tutorial.Backend ?? ServerLifecycleClient;

    private TimeSpan ActiveFinishHoldDuration =>
        Tutorial.IsActive ? TutorialFinishHoldDuration : FinishHoldDuration;

    private bool CanStartTutorial =>
        !Tutorial.IsActive && !_isLoading && !_isBusy && !_isFinishHoldActive && _completedHistory is null;

    /// <summary>
    /// Пока идёт обучение, «Назад» на телефоне принадлежит ему, какое бы окно
    /// демонстрация ни открыла.
    /// </summary>
    private void SetBackInterceptor(Action? interceptor) =>
        Tutorial.SetBackInterceptor(interceptor);

    private async Task StartTutorialAsync()
    {
        if (!CanStartTutorial)
            return;

        // Отметки идущей тренировки сохраняются до подмены: если система
        // закроет приложение посреди демонстрации, они не потеряются.
        await SaveSessionIfChangedAsync();

        var snapshot = TakeSnapshot();
        if (!Tutorial.Start(PlayFreeWorkoutAsync))
            return;

        _tutorialSnapshot = snapshot;
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
        StateHasChanged();
    }

    // Обучение закрывается: удержание «Завершить» прерывается сразу, пока
    // слой ещё гаснет.
    private void OnTutorialStopping() => _ = InvokeAsync(() =>
    {
        CancelFinishHold();
        StateHasChanged();
    });

    // Подставные данные убраны: возвращается отложенное состояние, а если
    // страницу создала проходка по шаблону — загружаются настоящие данные.
    private void OnTutorialStopped() => _ = InvokeAsync(async () =>
    {
        CloseTutorialDialogs();
        if (_tutorialSnapshot is { } snapshot)
        {
            _tutorialSnapshot = null;
            RestoreSnapshot(snapshot);
        }
        else
        {
            await LoadAsync();
        }

        StateHasChanged();
    });

    private async Task PlayFreeWorkoutAsync(CancellationToken token)
    {
        var backend = Tutorial.Backend!;
        await CardAsync(token, "Tutorial_Today_Intro", number: null);

        // 1. Первое упражнение — подробно: свободная тренировка, выбор,
        // подходы, вес и повторения, «Добавить упражнение».
        await CardAsync(token, "Tutorial_Today_Episode1", number: 1);
        await TapAsync(token, "[data-tour=\"free-start\"]", CreateFreeWorkoutAsync, after: 1300);
        await FillFirstExerciseAsync(token, backend.Definitions[0], setsCount: 3, [(60, 10), (70, 8), (75, 6)]);
        var benchId = _exercises[^1].Id;

        // 2. Ещё два упражнения: окно уже показано, поэтому рука только
        // нажимает «Добавить упражнение», и упражнение сразу на странице.
        await CardAsync(token, "Tutorial_Today_Episode2", number: 2);
        var rowId = await QuickAddExerciseAsync(token, backend.Definitions[1], [(24, 12), (26, 10)]);
        var dipsId = await QuickAddExerciseAsync(token, backend.Definitions[2], [(0, 12), (0, 10)]);

        // 3. Суперсет из двух последних упражнений.
        await CardAsync(token, "Tutorial_Today_Episode3", number: 3);
        await TapAsync(token, "[data-tour=\"superset-create\"]", () => { OpenSupersetDialog(); return Task.CompletedTask; }, after: 1100);
        await TapAsync(token, $"[data-tour=\"superset-option-{rowId}\"]", () => { ToggleSupersetExercise(rowId); return Task.CompletedTask; }, after: 800);
        await TapAsync(token, $"[data-tour=\"superset-option-{dipsId}\"]", () => { ToggleSupersetExercise(dipsId); return Task.CompletedTask; }, after: 900);
        await TapAsync(token, "[data-tour=\"superset-confirm\"]", CreateSupersetAsync);

        var superset = ExerciseGroups.First(group => group.IsSuperset);
        var supersetSelector = SupersetSelector(superset);
        await ShowAsync(token, supersetSelector, 3200, $"{supersetSelector} .superset-label");

        // 4. Выполнение: подробно только первое упражнение, к завершению
        // страница просто проматывается.
        await CardAsync(token, "Tutorial_Today_Episode4", number: 4);
        await PerformWorkoutAsync(token, benchId);

        // У свободной тренировки после удержания — вопрос, сделать ли из неё
        // шаблон. Его только показываем: отвечать за пользователя не нужно.
        await FocusAsync(token, "[data-tour=\"template-prompt\"]", null);
        await PauseAsync(3200, token);

        await CardAsync(token, "Tutorial_Today_Done", number: null, final: true);
    }

    private static string SupersetSelector(ExerciseGroup superset) =>
        $"[data-superset-id=\"{superset.Exercises[0].SupersetGroupId}\"]";

    /// <summary>
    /// Выполнение тренировки в бойком темпе: время отдыха первого упражнения,
    /// его подходы с ускоренными таймерами и оценка тяжести. Остальные
    /// упражнения не проходятся по шагам — к прокрутке они уже выполнены и
    /// оценены, страница плавно проматывается к «Завершить», дальше удержание
    /// до стандартной анимации.
    /// </summary>
    private async Task PerformWorkoutAsync(CancellationToken token, int benchId)
    {
        var bench = $"[data-tour-exercise=\"{benchId}\"]";
        await TapAsync(token, $"{bench} [data-tour=\"rest-1\"]",
            () => { OpenRestTimer(benchId, 1, RestTimerKind.BetweenSets); return Task.CompletedTask; }, after: 700, aim: 700);
        await TypeAsync(token, "[data-tour=\"rest-seconds\"]", "60", value => _restDraftSeconds = int.Parse(value), fast: true);
        await TapAsync(token, "[data-tour=\"rest-done\"]", CloseRestTimerAsync, after: 700, aim: 700);

        // Подходы первого упражнения с отдыхом между ними.
        var benchDraft = _exercises.First(x => x.Id == benchId);
        foreach (var set in benchDraft.Sets)
        {
            var row = $"{bench} [data-tour=\"set-{set.SetNumber}\"]";
            await TapAsync(token, row, () => SetCompletedAsync(benchDraft, set, new ChangeEventArgs { Value = true }),
                after: 500, finger: $"{row} .complete-control", aim: 650);

            if (HasFollowingSet(benchDraft, set))
            {
                await WaitRestAsync(token, $"{bench} [data-tour=\"rest-{set.SetNumber}\"]",
                    GetRestTimer(benchId, set.SetNumber, RestTimerKind.BetweenSets));
            }
        }

        // Оценка тяжести упражнения: над квадратами появляется подпись.
        var rating = $"{bench} .exercise-footer .effort-rating";
        await TapAsync(token, rating, () => { benchDraft.Status = "Hard"; return Task.CompletedTask; },
            after: 1200, finger: $"{rating} .effort-option--hard", aim: 700);

        // Остальные упражнения к прокрутке уже выполнены и оценены, их
        // отдых отмечен прошедшим.
        foreach (var exercise in _exercises.Where(x => x.Id != benchId))
        {
            foreach (var set in exercise.Sets)
            {
                set.Completed = true;
                GetRestTimer(exercise.Id, set.SetNumber, RestTimerKind.BetweenSets).IsCompleted = true;
            }
            GetRestTimer(exercise.Id, 0, RestTimerKind.BetweenExercises).IsCompleted = true;
            exercise.Status = "Medium";
        }
        StateHasChanged();

        // Завершение удержанием кнопки: фокус на ней проматывает страницу.
        await FocusAsync(token, "[data-tour=\"finish\"]", "[data-tour=\"finish\"]");
        await PauseAsync(1000, token);
        await Tutorial.Spotlight.PressAsync(true);
        // Пока палец держит кнопку, в фокусе стандартная анимация завершения:
        // она рисуется поверх страницы и иначе осталась бы под вуалью.
        await FocusAsync(token, ".finish-hold-animation", "[data-tour=\"finish\"]");
        try
        {
            await BeginFinishHoldAsync();
        }
        finally
        {
            await Tutorial.Spotlight.PressAsync(false);
        }
        token.ThrowIfCancellationRequested();
        Tutorial.Resync();
    }

    private Task CardAsync(CancellationToken token, string key, int? number, bool final = false) =>
        Tutorial.CardAsync(token, key, number, final);

    /// <summary>
    /// Первое упражнение — через окно нового упражнения, подробно: выбор из
    /// библиотеки, число подходов, вес и повторения набираются по цифре.
    /// </summary>
    private async Task FillFirstExerciseAsync(
        CancellationToken token,
        ExerciseDefinitionApiResponse definition,
        int setsCount,
        IReadOnlyList<(double Weight, int Repetitions)> sets)
    {
        // Список упражнений — системное окно телефона, его не показать изнутри
        // страницы, поэтому поле только «нажимается» и получает выбор.
        await TapAsync(token, "[data-tour=\"editor-exercise\"]",
            () => { SetFreeExerciseDefinition(definition.Id.ToString()); return Task.CompletedTask; }, after: 1200);

        await TypeAsync(token, "[data-tour=\"editor-sets\"]", setsCount.ToString(),
            value => _freeEditorDraft?.ResizeSets(int.Parse(value)));

        for (var index = 0; index < sets.Count; index++)
        {
            var draftSet = _freeEditorDraft!.Sets[index];
            var (weight, repetitions) = sets[index];
            // В фокусе весь подход, рука нажимает на поля внутри него.
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

        await TapAsync(token, "[data-tour=\"editor-save\"]", SaveFreeExerciseEdit, after: 1600);
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

    private Task FocusAsync(CancellationToken token, string selector, string? finger) =>
        Tutorial.FocusAsync(token, selector, finger);

    /// <summary>Подсветить элемент, «нажать» на него и выполнить то, что делает нажатие.</summary>
    private Task TapAsync(
        CancellationToken token,
        string selector,
        Func<Task> action,
        int after = 1200,
        string? finger = null,
        bool fast = false,
        int? aim = null) =>
        Tutorial.TapAsync(token, selector, async () =>
        {
            await action();
            StateHasChanged();
        }, after, finger, fast, aim);

    private Task TypeAsync(
        CancellationToken token,
        string selector,
        string value,
        Action<string> apply,
        bool fast = false,
        string? area = null) =>
        Tutorial.TypeAsync(token, selector, value, apply, StateHasChanged, fast, area);

    private Task ShowAsync(CancellationToken token, string selector, int duration, string finger) =>
        Tutorial.ShowAsync(token, selector, duration, finger);

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
        Tutorial.Resync();
        await PauseAsync(500, token);
    }

    private Task PauseAsync(int milliseconds, CancellationToken token) =>
        Tutorial.PauseAsync(milliseconds, token);

    private double RestTimeScale(int durationSeconds) =>
        Tutorial.IsActive ? Math.Max(1, durationSeconds / TutorialRestSeconds) : 1;

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
