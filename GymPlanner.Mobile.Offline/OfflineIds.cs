namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Временные идентификаторы для того, что создано на телефоне без связи.
/// </summary>
/// <remarks>
/// Серверные ID положительные, а небольшие отрицательные уже заняты: так экран
/// тренировки помечает ещё не сохранённые упражнения. Поэтому временные ID берутся
/// из дальнего отрицательного диапазона, где их не спутать ни с тем, ни с другим.
/// </remarks>
public static class OfflineIds
{
    private const int LocalThreshold = -1_000_000_000;

    public static bool IsLocal(int id) => id <= LocalThreshold;

    public static int Next() => LocalThreshold - Random.Shared.Next(0, 1_000_000_000);
}
