using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.ExerciseLibrary;

/// <summary>
/// Окно выбора упражнения из библиотеки: поиск и разделы по частям тела.
/// </summary>
/// <remarks>
/// Само окно рисует <c>ExercisePickerSheet</c> в общем макете: поле выбора стоит
/// внутри модальных окон страниц, а их <c>backdrop-filter</c> и анимация страницы
/// привязали бы fixed-окно к модальному, а не к экрану.
/// </remarks>
public sealed class ExercisePicker
{
    private TaskCompletionSource<int?>? _completion;

    public ExercisePickerRequest? Request { get; private set; }

    public event Action? Changed;

    /// <returns>Выбранное упражнение или <c>null</c>, если окно закрыли без выбора.</returns>
    public Task<int?> PickAsync(
        string title,
        IReadOnlyList<ExerciseDefinitionApiResponse> definitions,
        int? selectedId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(definitions);

        // Второе открытие поверх первого невозможно в интерфейсе, но если
        // случится, первое ожидание завершается без выбора, а не висит вечно.
        _completion?.TrySetResult(null);
        var completion = new TaskCompletionSource<int?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;
        Request = new ExercisePickerRequest(title, definitions, selectedId);
        Changed?.Invoke();
        return completion.Task;
    }

    public void Complete(int? definitionId)
    {
        var completion = _completion;
        _completion = null;
        Request = null;
        Changed?.Invoke();
        completion?.TrySetResult(definitionId);
    }
}

public sealed record ExercisePickerRequest(
    string Title,
    IReadOnlyList<ExerciseDefinitionApiResponse> Definitions,
    int? SelectedId);
