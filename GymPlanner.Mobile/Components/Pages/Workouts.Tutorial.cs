using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Tutorial;

namespace GymPlanner.Mobile.Components.Pages;

/// <summary>
/// Обучение «Тренировок»: как составить шаблон. Список создаёт демо-шаблон и
/// открывает его, дальше ролик продолжает редактор шаблона.
/// </summary>
/// <remarks>
/// Пока идёт обучение, список работает с <see cref="TutorialWorkoutBackend"/>:
/// он пуст, а созданный роликом шаблон живёт только в памяти. После обучения
/// список загружается заново — с настоящими шаблонами.
/// </remarks>
public partial class Workouts
{
    private IWorkoutApiClient WorkoutClient =>
        (IWorkoutApiClient?)Tutorial.Backend ?? ServerWorkoutClient;

    private bool CanStartTutorial => !Tutorial.IsActive && !_isLoading && !_isBusy;

    protected override void OnInitialized() => Tutorial.Stopped += OnTutorialStopped;

    private void StartTutorial()
    {
        if (!CanStartTutorial || !Tutorial.StartWorkouts(PlayTutorialAsync))
            return;

        // Под описанием уже демо-список: настоящие шаблоны вернутся после обучения.
        CloseMenu();
        _isCreateOpen = false;
        _newName = string.Empty;
        CancelEdit();
        _deleteTarget = null;
        _errors.Clear();
        _plans.Clear();
    }

    /// <summary>
    /// 1. Создаём шаблон: «Добавить шаблон», имя, «Сохранить» — и открываем
    /// его обычным нажатием на карточку. Эпизод простой и идёт в бойком темпе.
    /// </summary>
    private async Task PlayTutorialAsync(CancellationToken token)
    {
        await Tutorial.CardAsync(token, "Tutorial_Workouts_Episode1", 1);
        await TapAsync(token, "[data-tour=\"add-template\"]",
            () => { OpenCreateDialog(); return Task.CompletedTask; }, after: 500, aim: 800);
        await Tutorial.TypeAsync(token, "[data-tour=\"template-name\"]", Text["Tutorial_Today_TemplateName"],
            value => _newName = value, StateHasChanged, fast: true);
        await TapAsync(token, "[data-tour=\"template-save\"]", CreateAsync, after: 500, aim: 700);

        var card = $"[data-tour=\"template-card-{_plans[^1].Id}\"]";
        await Tutorial.ShowAsync(token, card, 900, card);
        // Карточка ведёт в редактор шаблона: там ролик и продолжится.
        await Tutorial.TapAsync(token, card, () => Tutorial.Spotlight.ClickAsync(card), after: 400, aim: 700);
    }

    private Task TapAsync(CancellationToken token, string selector, Func<Task> action, int after = 1200, int? aim = null) =>
        Tutorial.TapAsync(token, selector, async () =>
        {
            await action();
            StateHasChanged();
        }, after, aim: aim);

    private void OnTutorialStopped() => _ = InvokeAsync(async () =>
    {
        CloseMenu();
        _isCreateOpen = false;
        _newName = string.Empty;
        await LoadAsync();
        StateHasChanged();
    });

    public void Dispose() => Tutorial.Stopped -= OnTutorialStopped;
}
