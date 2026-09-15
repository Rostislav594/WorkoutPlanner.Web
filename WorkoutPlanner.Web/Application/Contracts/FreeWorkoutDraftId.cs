namespace WorkoutPlanner.Web.Application.Contracts;

/// <summary>
/// Идентификатор активной тренировки для свободного черновика.
/// </summary>
/// <remarks>
/// У запланированной тренировки идентификатор — это строка календаря
/// (<c>WorkoutDay.Id</c>), а у свободной такой строки нет: черновик живёт
/// планом и в календарь не попадает. Чтобы не заводить второе поле, которое
/// пришлось бы протаскивать через контракт часов и обратно, вид тренировки
/// закодирован в самом числе: у черновика идентификатор отрицательный и равен
/// минус идентификатору плана.
///
/// Так любой обработчик, получив число, однозначно понимает, с чем имеет дело,
/// и не может перепутать строку календаря с планом — их диапазоны не
/// пересекаются.
/// </remarks>
public static class FreeWorkoutDraftId
{
    /// <summary>Идентификатор активной тренировки для черновика плана.</summary>
    public static int FromPlanId(int planId) => -planId;

    /// <summary>Относится ли идентификатор к свободному черновику.</summary>
    public static bool IsDraft(int workoutId) => workoutId < 0;

    /// <summary>Идентификатор плана, стоящего за черновиком.</summary>
    public static int ToPlanId(int workoutId) => -workoutId;
}

/// <summary>Черновик свободной тренировки в терминах приложения.</summary>
public sealed record FreeWorkoutDraftResult(
    int TrainingPlanId,
    int WorkoutId,
    string WorkoutName,
    bool AlreadyStarted);
