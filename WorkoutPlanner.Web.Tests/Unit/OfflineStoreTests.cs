using System.Net;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class OfflineDocumentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gymplanner-offline-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Documents_AreKeptPerAccount_AndSurviveRestart()
    {
        var store = new OfflineDocumentStore(_root);
        await store.UseAccountAsync("first@example.test");
        await store.SetAsync(OfflineKeys.Profile, Profile("first@example.test"));
        await store.UseAccountAsync("second@example.test");
        Assert.Null(await store.GetAsync<ProfileResponse>(OfflineKeys.Profile));

        var restarted = new OfflineDocumentStore(_root);
        await restarted.UseAccountAsync("FIRST@example.test ");
        var profile = await restarted.GetAsync<ProfileResponse>(OfflineKeys.Profile);
        Assert.Equal("first@example.test", profile?.Email);
    }

    [Fact]
    public async Task WithoutAccount_NothingIsStored()
    {
        var store = new OfflineDocumentStore(_root);
        await store.SetAsync(OfflineKeys.Profile, Profile("nobody@example.test"));
        Assert.False(store.HasAccount);
        Assert.Null(await store.GetAsync<ProfileResponse>(OfflineKeys.Profile));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task CorruptDocument_IsTreatedAsMissing()
    {
        var store = new OfflineDocumentStore(_root);
        await store.UseAccountAsync("corrupt@example.test");
        await store.SetAsync(OfflineKeys.Profile, Profile("corrupt@example.test"));
        var file = Directory.GetFiles(_root, "profile.json", SearchOption.AllDirectories).Single();
        await File.WriteAllTextAsync(file, "{ not json");

        var restarted = new OfflineDocumentStore(_root);
        await restarted.UseAccountAsync("corrupt@example.test");
        Assert.Null(await restarted.GetAsync<ProfileResponse>(OfflineKeys.Profile));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task DeleteAccountData_RemovesOnlyThatAccount()
    {
        var store = new OfflineDocumentStore(_root);
        await store.UseAccountAsync("keep@example.test");
        await store.SetAsync(OfflineKeys.Profile, Profile("keep@example.test"));
        await store.UseAccountAsync("drop@example.test");
        await store.SetAsync(OfflineKeys.Profile, Profile("drop@example.test"));
        await store.WriteBytesAsync(OfflineKeys.ExercisePhoto(5), [1, 2, 3]);

        await store.DeleteAccountDataAsync();

        Assert.Null(await store.GetAsync<ProfileResponse>(OfflineKeys.Profile));
        Assert.Null(await store.ReadBytesAsync(OfflineKeys.ExercisePhoto(5)));
        await store.UseAccountAsync("keep@example.test");
        Assert.NotNull(await store.GetAsync<ProfileResponse>(OfflineKeys.Profile));
    }

    [Fact]
    public async Task RemoveByPrefix_ForgetsMatchingDocumentsOnly()
    {
        var store = new OfflineDocumentStore(_root);
        await store.UseAccountAsync("progress@example.test");
        await store.SetAsync(OfflineKeys.ProgressOverview, new ProgressOverviewApiResponse([], []));
        await store.SetAsync(OfflineKeys.ExerciseProgress(3, "Жим лёжа"), new ExerciseProgressApiResponse(3, "A", "Жим лёжа", []));
        await store.SetAsync(OfflineKeys.Profile, Profile("progress@example.test"));

        await store.RemoveByPrefixAsync(OfflineKeys.ProgressPrefix);

        var restarted = new OfflineDocumentStore(_root);
        await restarted.UseAccountAsync("progress@example.test");
        Assert.Null(await restarted.GetAsync<ProgressOverviewApiResponse>(OfflineKeys.ProgressOverview));
        Assert.Null(await restarted.GetAsync<ExerciseProgressApiResponse>(OfflineKeys.ExerciseProgress(3, "Жим лёжа")));
        Assert.NotNull(await restarted.GetAsync<ProfileResponse>(OfflineKeys.Profile));
    }

    [Fact]
    public async Task PlansAndCalendar_RoundTripThroughJson()
    {
        var store = new OfflineDocumentStore(_root);
        await store.UseAccountAsync("plans@example.test");
        var plans = OfflinePlans.Empty.WithListed([Plan(1, "A"), Plan(2, "B")]).Upsert(Plan(9, "Draft"), listed: false);
        await store.SetAsync(OfflineKeys.Plans, plans);
        await store.SetAsync(OfflineKeys.Calendar, OfflineCalendar.Empty.Upsert(Day(4, new DateTime(2026, 9, 25), 1)));

        var restarted = new OfflineDocumentStore(_root);
        await restarted.UseAccountAsync("plans@example.test");
        var loaded = await restarted.GetAsync<OfflinePlans>(OfflineKeys.Plans);
        Assert.Equal([1, 2], loaded!.Listed.Select(x => x.Id));
        Assert.Equal("Draft", loaded.Find(9)?.WorkoutName);
        Assert.Equal(12.5, loaded.Find(1)!.Exercises.Single().Sets.Single().Weight);
        var calendar = await restarted.GetAsync<OfflineCalendar>(OfflineKeys.Calendar);
        Assert.Equal(1, calendar!.Days[4].TrainingPlanId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    internal static ProfileResponse Profile(string email) => new(email, "Ann", "Lee", null, "Female", true);

    internal static TrainingPlanApiResponse Plan(int id, string name) =>
        new(id, name, new DateTime(2026, 1, 1), [Exercise(id * 100, id)]);

    internal static ExerciseApiResponse Exercise(int id, int planId, double weight = 12.5) =>
        new(id, "Squat", 1, "Normal", planId, null, false, [new ExerciseSetApiResponse(1, 10, weight, false)]);

    internal static WorkoutDayApiResponse Day(int id, DateTime date, int planId, bool completed = false) =>
        new(id, date, planId, completed);
}

public sealed class OfflineModelTests
{
    [Fact]
    public void WithListed_ReplacesListAndKeepsUnlistedPlans()
    {
        var plans = OfflinePlans.Empty
            .WithListed([OfflineDocumentStoreTests.Plan(1, "A"), OfflineDocumentStoreTests.Plan(2, "B")])
            .Upsert(OfflineDocumentStoreTests.Plan(9, "Draft"), listed: false);

        var refreshed = plans.WithListed([OfflineDocumentStoreTests.Plan(2, "B2"), OfflineDocumentStoreTests.Plan(3, "C")]);

        Assert.Equal([2, 3], refreshed.Listed.Select(x => x.Id));
        Assert.Null(refreshed.Find(1));
        Assert.Equal("B2", refreshed.Find(2)?.WorkoutName);
        Assert.NotNull(refreshed.Find(9));
    }

    [Fact]
    public void UpsertAndRemoveExercise_UpdateOwningPlan()
    {
        var plans = OfflinePlans.Empty.WithListed([OfflineDocumentStoreTests.Plan(1, "A")]);

        var updated = plans
            .UpsertExercise(OfflineDocumentStoreTests.Exercise(100, 1, weight: 40))
            .UpsertExercise(OfflineDocumentStoreTests.Exercise(101, 1));
        Assert.Equal(40, updated.FindExercise(100)!.Sets.Single().Weight);
        Assert.Equal(2, updated.Find(1)!.Exercises.Count);
        Assert.Equal([1], updated.Order);

        var removed = updated.RemoveExercise(100);
        Assert.Null(removed.FindExercise(100));
        Assert.NotNull(removed.FindExercise(101));

        Assert.Same(plans, plans.UpsertExercise(OfflineDocumentStoreTests.Exercise(500, 77)));
    }

    [Fact]
    public void CalendarRange_ReplacesOnlyThatPeriod()
    {
        var calendar = OfflineCalendar.Empty
            .Upsert(OfflineDocumentStoreTests.Day(1, new DateTime(2026, 8, 30), 1))
            .Upsert(OfflineDocumentStoreTests.Day(2, new DateTime(2026, 9, 10), 1))
            .Upsert(OfflineDocumentStoreTests.Day(3, new DateTime(2026, 10, 2), 1));

        var refreshed = calendar.WithRange(
            new DateTime(2026, 9, 1),
            new DateTime(2026, 9, 30),
            [OfflineDocumentStoreTests.Day(4, new DateTime(2026, 9, 12), 2)]);

        Assert.Equal([1, 3, 4], refreshed.Days.Keys.Order());
        Assert.Equal([4], refreshed.InRange(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)).Select(x => x.Id));
    }

    [Fact]
    public void CalendarUpsert_KeepsOneWorkoutPerDate_AndPlanRemovalDropsItsDays()
    {
        var date = new DateTime(2026, 9, 25);
        var calendar = OfflineCalendar.Empty
            .Upsert(OfflineDocumentStoreTests.Day(1, date, 1))
            .Upsert(OfflineDocumentStoreTests.Day(2, date.AddDays(1), 2))
            .Upsert(OfflineDocumentStoreTests.Day(3, date, 2));

        Assert.Equal([2, 3], calendar.Days.Keys.Order());
        Assert.Empty(calendar.RemovePlan(2).Days);
    }

    [Fact]
    public void TodayResolver_PicksTodaysIncompleteWorkoutWithItsPlan()
    {
        var today = new DateTime(2026, 9, 25, 18, 30, 0);
        var plans = OfflinePlans.Empty.WithListed([OfflineDocumentStoreTests.Plan(1, "Legs"), OfflineDocumentStoreTests.Plan(2, "Arms")]);
        var calendar = OfflineCalendar.Empty
            .Upsert(OfflineDocumentStoreTests.Day(10, today.Date.AddDays(-1), 2))
            .Upsert(OfflineDocumentStoreTests.Day(11, today.Date, 1));

        var resolved = TodayWorkoutResolver.Resolve(calendar, plans, today);
        Assert.Equal(11, resolved?.Day.Id);
        Assert.Equal("Legs", resolved?.TrainingPlan.WorkoutName);

        var completed = calendar.Upsert(OfflineDocumentStoreTests.Day(11, today.Date, 1, completed: true));
        Assert.Null(TodayWorkoutResolver.Resolve(completed, plans, today));
        Assert.Null(TodayWorkoutResolver.Resolve(calendar, OfflinePlans.Empty, today));
    }
}

public sealed class ServerReachabilityTests
{
    [Fact]
    public void FailureMakesOffline_UntilServerAnswersAgain()
    {
        var reachability = new ServerReachability(TimeProvider.System);
        var changes = 0;
        reachability.Changed += () => changes++;
        Assert.False(reachability.IsOffline);

        reachability.ReportServerUnavailable();
        Assert.True(reachability.IsOffline);

        reachability.ReportServerResponded();
        Assert.False(reachability.IsOffline);
        Assert.NotNull(reachability.LastResponseUtc);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void NoNetwork_IsOffline_UntilServerResponds()
    {
        var reachability = new ServerReachability(TimeProvider.System);
        reachability.SetNetworkAvailable(false);
        Assert.True(reachability.IsOffline);

        reachability.ReportServerResponded();
        Assert.False(reachability.IsOffline);
    }

    [Fact]
    public async Task Handler_TurnsTimeoutIntoNetworkError_AndMarksScope()
    {
        var reachability = new ServerReachability(TimeProvider.System);
        using var client = new HttpClient(new ReachabilityHttpHandler(reachability)
        {
            InnerHandler = new StubHandler(async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        })
        {
            BaseAddress = new Uri("https://example.test/")
        };

        using var scope = TransportScope.Begin();
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/history");
        var started = DateTime.UtcNow;
        // Таймаут чтения — 10 секунд; тест ждёт его целиком, чтобы проверить сам механизм.
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request));
        Assert.True(DateTime.UtcNow - started >= ReachabilityHttpHandler.ReadTimeout - TimeSpan.FromSeconds(1));
        Assert.True(scope.ServerUnavailable);
        Assert.True(reachability.IsOffline);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, true, true)]
    [InlineData(HttpStatusCode.InternalServerError, true, false)]
    [InlineData(HttpStatusCode.Conflict, false, false)]
    public async Task Handler_ClassifiesServerResponses(HttpStatusCode status, bool scopeUnavailable, bool offline)
    {
        var reachability = new ServerReachability(TimeProvider.System);
        using var client = new HttpClient(new ReachabilityHttpHandler(reachability)
        {
            InnerHandler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(status)))
        })
        {
            BaseAddress = new Uri("https://example.test/")
        };

        using var scope = TransportScope.Begin();
        using var response = await client.PostAsync("api/v1/calendar", null);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(scopeUnavailable, scope.ServerUnavailable);
        Assert.Equal(offline, reachability.IsOffline);
    }

    [Fact]
    public async Task Handler_LetsCallerCancellationThrough()
    {
        var reachability = new ServerReachability(TimeProvider.System);
        using var client = new HttpClient(new ReachabilityHttpHandler(reachability)
        {
            InnerHandler = new StubHandler(async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        })
        {
            BaseAddress = new Uri("https://example.test/")
        };

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAsync("api/v1/history", cancellation.Token));
        Assert.False(reachability.IsOffline);
    }

    private sealed class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(cancellationToken);
    }
}
