using System.Net;
using GymPlanner.Mobile.Offline;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class LocalIdMapTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gymplanner-idmap-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Rewrite_ReplacesOnlyKnownLocalIds()
    {
        var plan = OfflineIds.Next();
        var unknown = OfflineIds.Next();
        var map = new Dictionary<int, int?> { [plan] = 42 };

        Assert.Equal("api/v1/training-plans/42/exercises", LocalIdMap.Rewrite($"api/v1/training-plans/{plan}/exercises", map));
        Assert.Equal(
            $"{{\"date\":\"2026-09-28\",\"trainingPlanId\":42,\"other\":{unknown},\"weight\":-1.5}}",
            LocalIdMap.Rewrite($"{{\"date\":\"2026-09-28\",\"trainingPlanId\":{plan},\"other\":{unknown},\"weight\":-1.5}}", map));
        Assert.Equal([plan, unknown], LocalIdMap.FindLocalIds($"{plan} and {unknown} but not -1 or 1000000000").ToArray());
    }

    [Fact]
    public void ReferencesRejected_WhenParentCreationFailed()
    {
        var plan = OfflineIds.Next();
        var operation = new OutboxOperation(Guid.NewGuid(), OutboxKinds.CreateExercise, "POST", $"api/v1/training-plans/{plan}/exercises", "{}", DateTimeOffset.UtcNow);

        Assert.True(LocalIdMap.ReferencesRejected(operation, new Dictionary<int, int?> { [plan] = null }));
        Assert.False(LocalIdMap.ReferencesRejected(operation, new Dictionary<int, int?> { [plan] = 7 }));
        Assert.False(LocalIdMap.ReferencesRejected(operation, new Dictionary<int, int?>()));
    }

    [Fact]
    public void ReadCreatedId_UnderstandsPlainAndFreeWorkoutResponses()
    {
        Assert.Equal(5, LocalIdMap.ReadCreatedId(OutboxKinds.CreatePlan, "{\"id\":5,\"workoutName\":\"A\"}"));
        Assert.Equal(9, LocalIdMap.ReadCreatedId(OutboxKinds.CompleteFreeWorkout, "{\"history\":{\"id\":9},\"trainingPlanId\":null}"));
        Assert.Null(LocalIdMap.ReadCreatedId(OutboxKinds.CreatePlan, "not json"));
        Assert.Null(LocalIdMap.ReadCreatedId(OutboxKinds.CreatePlan, "[]"));
    }

    [Fact]
    public async Task Outbox_SendsDependentOperationsWithTheRealId()
    {
        var plan = OfflineIds.Next();
        var paths = new List<string>();
        var bodies = new List<string>();
        var server = new Server(async request =>
        {
            paths.Add(request.RequestUri!.PathAndQuery.TrimStart('/'));
            bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync());
            return request.RequestUri!.AbsolutePath.EndsWith("training-plans", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":77}") }
                : new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":500}") };
        });
        var (outbox, reachability) = await CreateAsync(server);
        reachability.ReportServerUnavailable();
        await outbox.EnqueueAsync(outbox.Create(OutboxKinds.CreatePlan, HttpMethod.Post, "api/v1/training-plans", "{\"workoutName\":\"Legs\"}", "Legs", plan));
        await outbox.EnqueueAsync(outbox.Create(OutboxKinds.CreateExercise, HttpMethod.Post, $"api/v1/training-plans/{plan}/exercises", "{}", "Squat", OfflineIds.Next()));
        await outbox.EnqueueAsync(outbox.Create(OutboxKinds.ScheduleWorkout, HttpMethod.Post, "api/v1/calendar", $"{{\"trainingPlanId\":{plan}}}", "Legs", OfflineIds.Next()));
        reachability.ReportServerResponded();

        Assert.Equal(3, await outbox.FlushAsync());

        Assert.Equal(["api/v1/training-plans", "api/v1/training-plans/77/exercises", "api/v1/calendar"], paths);
        Assert.Equal("{\"trainingPlanId\":77}", bodies[2]);
        Assert.Equal(77, await outbox.ResolveAsync(plan));
    }

    [Fact]
    public async Task Outbox_DropsDependentsOfRejectedCreation_WithoutAskingTheServer()
    {
        var plan = OfflineIds.Next();
        var requests = 0;
        var server = new Server(_ =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("{\"title\":\"Name taken\"}") });
        });
        var (outbox, reachability) = await CreateAsync(server);
        reachability.ReportServerUnavailable();
        await outbox.EnqueueAsync(outbox.Create(OutboxKinds.CreatePlan, HttpMethod.Post, "api/v1/training-plans", "{}", "Legs", plan));
        await outbox.EnqueueAsync(outbox.Create(OutboxKinds.RenamePlan, HttpMethod.Put, $"api/v1/training-plans/{plan}", "{}", "Legs 2"));
        reachability.ReportServerResponded();

        Assert.Equal(2, await outbox.FlushAsync());
        Assert.Equal(1, requests);
        Assert.Empty(await outbox.GetPendingAsync());
    }

    private async Task<(OutboxSync Outbox, ServerReachability Reachability)> CreateAsync(HttpMessageHandler server)
    {
        var store = new OfflineDocumentStore(_root);
        await store.UseAccountAsync("idmap@example.test");
        var reachability = new ServerReachability(TimeProvider.System);
        var client = new HttpClient(server) { BaseAddress = new Uri("https://example.test/") };
        return (new OutboxSync(store, client, reachability, TimeProvider.System), reachability);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class Server(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request);
    }
}
