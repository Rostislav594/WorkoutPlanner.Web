using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>Состояние идущей тренировки на экране «Сегодня».</summary>
/// <param name="WorkoutDayId">День календаря; <c>null</c> — свободная тренировка.</param>
public sealed record ActiveWorkoutSnapshot(
    int TrainingPlanId,
    int? WorkoutDayId,
    DateTimeOffset SavedAtUtc,
    IReadOnlyList<ExerciseApiResponse> Exercises);

/// <summary>
/// Хранит на телефоне подходы идущей тренировки до её завершения.
/// </summary>
/// <remarks>
/// Раньше веса и отметки жили только в памяти страницы: закрытое системой
/// приложение теряло всю тренировку. Теперь она переживает перезапуск, а
/// старый снимок (вчерашний, чужой тренировки) просто не подхватывается.
/// </remarks>
public sealed class ActiveWorkoutSessionStore(OfflineRuntime runtime, TimeProvider timeProvider)
{
    private const string Key = "active-workout";
    private static readonly TimeSpan MaximumAge = TimeSpan.FromHours(8);

    public Task SaveAsync(int trainingPlanId, int? workoutDayId, IReadOnlyList<ExerciseApiResponse> exercises) =>
        runtime.UpdateCopyAsync<ActiveWorkoutSnapshot>(
            Key,
            _ => new ActiveWorkoutSnapshot(trainingPlanId, workoutDayId, timeProvider.GetUtcNow(), exercises));

    /// <summary>Снимок этой же тренировки, если он свежий.</summary>
    public async Task<ActiveWorkoutSnapshot?> LoadAsync(int trainingPlanId, int? workoutDayId)
    {
        var snapshot = await runtime.LoadCopyAsync<ActiveWorkoutSnapshot>(Key);
        return snapshot is not null &&
               snapshot.TrainingPlanId == trainingPlanId &&
               snapshot.WorkoutDayId == workoutDayId &&
               timeProvider.GetUtcNow() - snapshot.SavedAtUtc < MaximumAge
            ? snapshot
            : null;
    }

    public Task ClearAsync() => runtime.RemoveCopyAsync(Key);
}
