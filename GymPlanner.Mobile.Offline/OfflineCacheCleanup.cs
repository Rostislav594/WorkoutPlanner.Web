using System.Globalization;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Убирает из копии то, чего на сервере уже нет: фото и графики удалённых
/// упражнений и планов, старые черновики свободных тренировок.
/// </summary>
/// <remarks>
/// Копия перезаписывается при каждом обновлении и сама не растёт, но файлы,
/// привязанные к ID, остаются после удаления плана или упражнения. Запускать
/// только после свежей загрузки с сервера и при пустой очереди: иначе можно
/// выбросить то, что ещё не отправлено.
/// </remarks>
public static class OfflineCacheCleanup
{
    private const string PhotoTypePrefix = "photo-type-";
    private const string PhotoPrefix = "photo-";
    private const string WorkoutProgressPrefix = "progress-workout-";
    private const string WorkoutExercisesPrefix = "progress-exercises-";
    private const string ExerciseProgressPrefix = "progress-exercise-";

    /// <returns>Сколько записей удалено.</returns>
    public static async Task<int> RunAsync(
        OfflineDocumentStore store,
        int? activeDraftPlanId,
        CancellationToken cancellationToken = default)
    {
        var removed = 0;
        var plans = await store.GetAsync<OfflinePlans>(OfflineKeys.Plans, cancellationToken);
        if (plans is null)
            return removed;

        var pruned = PrunePlans(plans, activeDraftPlanId);
        if (pruned.Plans.Count != plans.Plans.Count)
        {
            removed += plans.Plans.Count - pruned.Plans.Count;
            await store.SetAsync(OfflineKeys.Plans, pruned, cancellationToken);
        }

        var overview = await store.GetAsync<ProgressOverviewApiResponse>(OfflineKeys.ProgressOverview, cancellationToken);
        var progressPlanIds = pruned.Plans.Keys
            .Concat(overview?.Workouts.Select(x => x.TrainingPlanId) ?? [])
            .ToHashSet();

        var keys = await store.ListKeysAsync(cancellationToken);
        var exerciseNames = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var planId in progressPlanIds)
        {
            if (keys.Contains(OfflineKeys.WorkoutExercises(planId)) &&
                await store.GetAsync<List<string>>(OfflineKeys.WorkoutExercises(planId), cancellationToken) is { } names)
            {
                exerciseNames[planId] = names;
            }
        }

        var exerciseIds = pruned.Plans.Values.SelectMany(x => x.Exercises).Select(x => x.Id).ToHashSet();
        foreach (var key in FindStaleKeys(keys, exerciseIds, progressPlanIds, exerciseNames))
        {
            await store.RemoveAsync(key, cancellationToken);
            await store.RemoveBytesAsync(key, cancellationToken);
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// Список «Мои тренировки» остаётся как есть; из планов вне списка остаётся
    /// только черновик идущей свободной тренировки.
    /// </summary>
    public static OfflinePlans PrunePlans(OfflinePlans plans, int? activeDraftPlanId)
    {
        var kept = plans.Plans
            .Where(x => plans.Order.Contains(x.Key) || x.Key == activeDraftPlanId)
            .ToDictionary(x => x.Key, x => x.Value);
        return kept.Count == plans.Plans.Count ? plans : new OfflinePlans(plans.Order, kept);
    }

    /// <param name="exerciseNames">
    /// Упражнения с графиками по планам, если их список сохранён; без списка
    /// графики упражнений плана не трогаются.
    /// </param>
    public static IReadOnlyList<string> FindStaleKeys(
        IEnumerable<string> keys,
        IReadOnlySet<int> exerciseIds,
        IReadOnlySet<int> planIds,
        IReadOnlyDictionary<int, IReadOnlyList<string>> exerciseNames)
    {
        var expectedExerciseKeys = exerciseNames
            .SelectMany(plan => plan.Value.Select(name => OfflineKeys.ExerciseProgress(plan.Key, name)))
            .ToHashSet(StringComparer.Ordinal);

        return keys.Where(IsStale).ToList();

        bool IsStale(string key)
        {
            // Порядок важен: «photo-type-» тоже начинается с «photo-».
            if (key.StartsWith(PhotoTypePrefix, StringComparison.Ordinal))
                return TryReadId(key[PhotoTypePrefix.Length..], out var id) && !exerciseIds.Contains(id);
            if (key.StartsWith(PhotoPrefix, StringComparison.Ordinal))
                return TryReadId(key[PhotoPrefix.Length..], out var id) && !exerciseIds.Contains(id);
            if (key.StartsWith(WorkoutProgressPrefix, StringComparison.Ordinal))
                return TryReadId(key[WorkoutProgressPrefix.Length..], out var id) && !planIds.Contains(id);
            if (key.StartsWith(WorkoutExercisesPrefix, StringComparison.Ordinal))
                return TryReadId(key[WorkoutExercisesPrefix.Length..], out var id) && !planIds.Contains(id);
            if (key.StartsWith(ExerciseProgressPrefix, StringComparison.Ordinal))
            {
                var rest = key[ExerciseProgressPrefix.Length..];
                var dash = rest.IndexOf('-');
                if (dash <= 0 || !TryReadId(rest[..dash], out var planId))
                    return false;
                if (!planIds.Contains(planId))
                    return true;
                return exerciseNames.ContainsKey(planId) && !expectedExerciseKeys.Contains(key);
            }

            return false;
        }
    }

    // Временные ID отрицательные, но до сервера они не дошли — такие ключи не трогаем.
    private static bool TryReadId(string value, out int id) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id);
}
