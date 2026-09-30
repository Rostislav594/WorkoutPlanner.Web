using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class OfflineCacheCleanupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gymplanner-cleanup-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RemovesPhotosAndProgressOfDeletedPlansAndExercises()
    {
        var store = await CreateStoreAsync();
        await store.SetAsync(OfflineKeys.Plans, new OfflinePlans([1], new Dictionary<int, TrainingPlanApiResponse>
        {
            [1] = Plan(1, 10)
        }));
        await store.SetAsync(OfflineKeys.ProgressOverview, new ProgressOverviewApiResponse([], []));
        await store.WriteBytesAsync(OfflineKeys.ExercisePhoto(10), [1]);
        await store.SetAsync(OfflineKeys.ExercisePhotoType(10), "image/jpeg");
        await store.WriteBytesAsync(OfflineKeys.ExercisePhoto(20), [1]);
        await store.SetAsync(OfflineKeys.ExercisePhotoType(20), "image/jpeg");
        await store.SetAsync(OfflineKeys.WorkoutProgress(1), "kept");
        await store.SetAsync(OfflineKeys.WorkoutProgress(2), "stale");
        await store.SetAsync(OfflineKeys.WorkoutExercises(1), new List<string> { "Squat" });
        await store.SetAsync(OfflineKeys.WorkoutExercises(2), new List<string> { "Bench" });
        await store.SetAsync(OfflineKeys.ExerciseProgress(1, "Squat"), "kept");
        await store.SetAsync(OfflineKeys.ExerciseProgress(1, "Renamed away"), "stale");
        await store.SetAsync(OfflineKeys.ExerciseProgress(2, "Bench"), "stale");

        var removed = await OfflineCacheCleanup.RunAsync(store, activeDraftPlanId: null);

        Assert.Equal(6, removed);
        var keys = (await store.ListKeysAsync()).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(
            new[]
            {
                OfflineKeys.ExercisePhoto(10),
                OfflineKeys.ExercisePhotoType(10),
                OfflineKeys.Plans,
                OfflineKeys.ProgressOverview,
                OfflineKeys.ExerciseProgress(1, "Squat"),
                OfflineKeys.WorkoutExercises(1),
                OfflineKeys.WorkoutProgress(1)
            }.Order(StringComparer.Ordinal),
            keys);
        Assert.Null(await store.ReadBytesAsync(OfflineKeys.ExercisePhoto(20)));
    }

    [Fact]
    public async Task DropsOldFreeWorkoutDrafts_KeepsTheActiveOne()
    {
        var store = await CreateStoreAsync();
        var activeDraft = OfflineIds.Next();
        await store.SetAsync(OfflineKeys.Plans, new OfflinePlans([1], new Dictionary<int, TrainingPlanApiResponse>
        {
            [1] = Plan(1, 10),
            [5] = Plan(5, 50),
            [activeDraft] = Plan(activeDraft, 60)
        }));
        await store.WriteBytesAsync(OfflineKeys.ExercisePhoto(50), [1]);

        await OfflineCacheCleanup.RunAsync(store, activeDraft);

        var plans = await store.GetAsync<OfflinePlans>(OfflineKeys.Plans);
        // Временный ID черновика отрицательный — при сортировке он первый.
        Assert.Equal([activeDraft, 1], plans!.Plans.Keys.Order().ToArray());
        Assert.Equal([1], plans.Order);
        Assert.Null(await store.ReadBytesAsync(OfflineKeys.ExercisePhoto(50)));
    }

    [Fact]
    public void KeepsProgressOfPlansKnownOnlyFromTheOverview_AndIgnoresOtherDocuments()
    {
        var stale = OfflineCacheCleanup.FindStaleKeys(
            [OfflineKeys.WorkoutProgress(7), OfflineKeys.ExerciseProgress(7, "Row"), OfflineKeys.ProgressOverview,
             OfflineKeys.History, "photo-type--1000000001", "photo-abc"],
            exerciseIds: new HashSet<int>(),
            planIds: new HashSet<int> { 7 },
            exerciseNames: new Dictionary<int, IReadOnlyList<string>>());

        Assert.Empty(stale);
    }

    [Fact]
    public async Task DoesNothingWithoutSavedPlans()
    {
        var store = await CreateStoreAsync();
        await store.WriteBytesAsync(OfflineKeys.ExercisePhoto(20), [1]);

        Assert.Equal(0, await OfflineCacheCleanup.RunAsync(store, null));
        Assert.NotNull(await store.ReadBytesAsync(OfflineKeys.ExercisePhoto(20)));
    }

    private static TrainingPlanApiResponse Plan(int id, int exerciseId) =>
        new(id, "Plan " + id, new DateTime(2026, 9, 29), [
            new ExerciseApiResponse(exerciseId, "Squat", 1, "Pending", id, null, true, [])
        ]);

    private async Task<OfflineDocumentStore> CreateStoreAsync()
    {
        var store = new OfflineDocumentStore(_root);
        await store.UseAccountAsync("cleanup@example.test");
        return store;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
