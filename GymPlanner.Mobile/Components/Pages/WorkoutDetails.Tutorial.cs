using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Tutorial;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Components.Pages;

/// <summary>
/// Вторая часть ролика «Тренировок»: в открытый роликом шаблон добавляются
/// упражнения и суперсет — теми же обработчиками, что и у кнопок редактора.
/// </summary>
/// <remarks>
/// Пока идёт обучение, редактор работает с <see cref="TutorialWorkoutBackend"/>,
/// и шаблон живёт только в памяти. Когда ролик закрывается, обучение уводит
/// обратно к настоящему списку шаблонов.
/// </remarks>
public partial class WorkoutDetails
{
    private bool _tutorialContinued;

    private IWorkoutApiClient WorkoutClient =>
        (IWorkoutApiClient?)Tutorial.Backend ?? ServerWorkoutClient;

    // Ролик «Тренировок» открыл этот шаблон: как только он загрузился,
    // редактор продолжает ролик своими действиями.
    private void ContinueTutorial()
    {
        if (_tutorialContinued || _loading || _plan is null || Tutorial.Scenario != TutorialScenario.Workouts)
            return;

        _tutorialContinued = true;
        Tutorial.ContinueOnPage(TutorialScenario.Workouts, PlayTutorialAsync);
    }

    private async Task PlayTutorialAsync(CancellationToken token)
    {
        var definitions = Tutorial.Backend!.Definitions;

        // 2. Упражнения: первое — подробно в окне, с разминочным подходом,
        // следующие два — одним нажатием «Добавить упражнение».
        await Tutorial.CardAsync(token, "Tutorial_Workouts_Episode2", 2);
        await TapAsync(token, "[data-tour=\"add-exercise\"]",
            () => { BeginCreate(); return Task.CompletedTask; }, after: 900);
        // Список упражнений — системное окно телефона, его не показать изнутри
        // страницы, поэтому поле только «нажимается» и получает выбор.
        await TapAsync(token, "[data-tour=\"editor-exercise\"]",
            () => { SetDefinition(definitions[3].Id.ToString()); return Task.CompletedTask; }, after: 1000);

        (double Weight, int Repetitions)[] benchSets = [(40, 12), (60, 10), (70, 8), (75, 6)];
        await TypeAsync(token, "[data-tour=\"editor-sets\"]", benchSets.Length.ToString(),
            value => ChangeSetsCount(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = value }));
        for (var index = 0; index < benchSets.Length; index++)
        {
            var set = _draft.Sets[index];
            var (weight, repetitions) = benchSets[index];
            var area = $"[data-tour=\"editor-set-{set.SetNumber}\"]";
            // Первый подход — подробно, остальные быстрее.
            var fast = index > 0;
            await TypeAsync(token, $"[data-tour=\"editor-weight-{set.SetNumber}\"]",
                weight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                value => set.Weight = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                fast, area);
            await TypeAsync(token, $"[data-tour=\"editor-reps-{set.SetNumber}\"]",
                repetitions.ToString(),
                value => set.Repetitions = int.Parse(value),
                fast, area);
        }

        await TapAsync(token, "[data-tour=\"editor-save\"]", SaveAsync, after: 1200);

        // Первый подход — разминочный: нажатие на его номер меняет вид подхода.
        var bench = _plan!.Exercises[^1];
        var benchCard = $"[data-tour-exercise=\"{bench.Id}\"]";
        await TapAsync(token, $"{benchCard} .exercise-set-row",
            () => ToggleSetKindAsync(bench, bench.Sets.OrderBy(x => x.SetNumber).First()),
            after: 1400, finger: $"{benchCard} .set-number");

        var rowId = await QuickAddExerciseAsync(token, definitions[1], [(24, 12), (26, 10)]);
        var dipsId = await QuickAddExerciseAsync(token, definitions[2], [(0, 12), (0, 10)]);

        // 3. Фото настроек тренажёра: угол спинки, отверстие стойки, ступень
        // упора — чтобы в следующий раз выставить всё так же.
        await Tutorial.CardAsync(token, "Tutorial_Workouts_Episode3", 3);
        await TapAsync(token, $"{benchCard} .exercise-photo-button",
            () => OpenPhotoAsync(_plan!.Exercises.First(x => x.Id == bench.Id)), after: 600);
        await TapAsync(token, ".photo-modal .photo-actions .secondary-button",
            () => { ShowTutorialPhoto(bench.Id); return Task.CompletedTask; }, after: 0, fast: true);
        // Рука убрана: снимок должен быть виден целиком.
        await Tutorial.FocusAsync(token, ".photo-modal .exercise-photo", null);
        await Tutorial.PauseAsync(3200, token);
        await TapAsync(token, ".photo-modal .icon-close-button",
            () => { ClosePhoto(); return Task.CompletedTask; }, after: 0, fast: true);
        // Миниатюра теперь в карточке; рука не закрывает её.
        await Tutorial.FocusAsync(token, benchCard, null);
        await Tutorial.PauseAsync(1500, token);

        // 4. Суперсет из двух последних упражнений.
        await Tutorial.CardAsync(token, "Tutorial_Workouts_Episode4", 4);
        await TapAsync(token, "[data-tour=\"superset-create\"]",
            () => { OpenSupersetDialog(); return Task.CompletedTask; }, after: 1100);
        await TapAsync(token, $"[data-tour=\"superset-option-{rowId}\"]",
            () => { ToggleSupersetExercise(rowId); return Task.CompletedTask; }, after: 800);
        await TapAsync(token, $"[data-tour=\"superset-option-{dipsId}\"]",
            () => { ToggleSupersetExercise(dipsId); return Task.CompletedTask; }, after: 900);
        await TapAsync(token, "[data-tour=\"superset-confirm\"]", CreateSupersetAsync);

        var superset = "[data-superset-id]";
        await Tutorial.ShowAsync(token, superset, 3200, $"{superset} .template-superset-label");
    }

    /// <summary>
    /// Окно уже показано на первом упражнении, поэтому рука только нажимает
    /// «Добавить упражнение», и упражнение сразу появляется в шаблоне.
    /// </summary>
    /// <returns>Идентификатор добавленного упражнения.</returns>
    private async Task<int> QuickAddExerciseAsync(
        CancellationToken token,
        ExerciseDefinitionApiResponse definition,
        IReadOnlyList<(double Weight, int Repetitions)> sets)
    {
        await TapAsync(token, "[data-tour=\"add-exercise\"]", () => AddExerciseWithoutEditorAsync(definition, sets), after: 0);
        var exerciseId = _plan!.Exercises[^1].Id;

        // Новая карточка встаёт в конец списка: страница докручивается к ней.
        var card = $"[data-tour-exercise=\"{exerciseId}\"]";
        await Tutorial.ShowAsync(token, card, 1500, $"{card} .exercise-card-header");
        return exerciseId;
    }

    /// <summary>
    /// Черновик заполняется сразу и сохраняется тем же <see cref="SaveAsync"/>,
    /// что и кнопка окна, — только окно не открывается.
    /// </summary>
    private async Task AddExerciseWithoutEditorAsync(
        ExerciseDefinitionApiResponse definition,
        IReadOnlyList<(double Weight, int Repetitions)> sets)
    {
        _editingId = null;
        _draft = ExerciseDraft.Create();
        _editorErrors.Clear();
        SetDefinition(definition.Id.ToString());
        _draft.Resize(sets.Count);
        for (var index = 0; index < sets.Count; index++)
        {
            _draft.Sets[index].Weight = sets[index].Weight;
            _draft.Sets[index].Repetitions = sets[index].Repetitions;
        }

        await SaveAsync();
    }

    /// <summary>
    /// «Снимок» тренажёра: вместо камеры — демо-картинка, и только в окне и
    /// миниатюре этой страницы. Отметка о фото у упражнения не ставится, чтобы
    /// страница не пошла за снимком на сервер.
    /// </summary>
    private void ShowTutorialPhoto(int exerciseId)
    {
        const string photo = "images/tutorial-incline-bench.svg";
        _photoDataUri = photo;
        _photoThumbnails[exerciseId] = photo;
    }

    private Task TapAsync(
        CancellationToken token,
        string selector,
        Func<Task> action,
        int after = 1200,
        string? finger = null,
        bool fast = false) =>
        Tutorial.TapAsync(token, selector, async () =>
        {
            await action();
            StateHasChanged();
        }, after, finger, fast);

    private Task TypeAsync(
        CancellationToken token,
        string selector,
        string value,
        Action<string> apply,
        bool fast = false,
        string? area = null) =>
        Tutorial.TypeAsync(token, selector, value, apply, StateHasChanged, fast, area);
}
