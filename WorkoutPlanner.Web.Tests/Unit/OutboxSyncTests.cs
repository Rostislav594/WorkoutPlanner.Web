using System.Net;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class OutboxSyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gymplanner-outbox-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Offline_QueuesWithoutSending_AndFlushSendsInOrderWithKeys()
    {
        var server = new RecordingServer(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var (outbox, reachability) = await CreateAsync(server);
        reachability.ReportServerUnavailable();

        var first = outbox.Create(OutboxKinds.UpdateExercise, HttpMethod.Put, "api/v1/exercises/1", "{}");
        var second = outbox.Create(OutboxKinds.DeleteExercise, HttpMethod.Delete, "api/v1/exercises/2", null);
        Assert.Equal(OutboxOutcomeKind.Queued, (await outbox.SubmitAsync(first, TimeSpan.FromSeconds(5))).Kind);
        Assert.Equal(OutboxOutcomeKind.Queued, (await outbox.SubmitAsync(second, TimeSpan.FromSeconds(5))).Kind);
        Assert.Empty(server.Requests);
        Assert.Equal(2, (await outbox.GetPendingAsync()).Count);

        reachability.ReportServerResponded();
        var flushed = 0;
        outbox.Flushed += count => flushed = count;
        Assert.Equal(2, await outbox.FlushAsync());

        Assert.Equal(["PUT api/v1/exercises/1", "DELETE api/v1/exercises/2"], server.Requests.Select(x => x.Line));
        Assert.Equal([first.Id.ToString(), second.Id.ToString()], server.Requests.Select(x => x.Key));
        Assert.Empty(await outbox.GetPendingAsync());
        Assert.Equal(2, flushed);
    }

    [Fact]
    public async Task Online_SubmitReturnsServerAnswer()
    {
        var server = new RecordingServer(_ => new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":5}") });
        var (outbox, _) = await CreateAsync(server);

        var outcome = await outbox.SubmitAsync(
            outbox.Create(OutboxKinds.CompleteScheduledWorkout, HttpMethod.Post, "api/v1/workouts/days/1/complete", "{}"),
            TimeSpan.FromSeconds(5));

        Assert.Equal(OutboxOutcomeKind.Sent, outcome.Kind);
        Assert.Equal("{\"id\":5}", outcome.Body);
        Assert.Empty(await outbox.GetPendingAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task TemporaryFailure_KeepsOperationQueued(HttpStatusCode status)
    {
        var server = new RecordingServer(_ => new HttpResponseMessage(status) { Content = new StringContent("{}") });
        var (outbox, _) = await CreateAsync(server);

        var outcome = await outbox.SubmitAsync(
            outbox.Create(OutboxKinds.CompleteFreeWorkout, HttpMethod.Post, "api/v1/workouts/free/complete", "{}"),
            TimeSpan.FromSeconds(5));

        Assert.Equal(OutboxOutcomeKind.Queued, outcome.Kind);
        Assert.Single(await outbox.GetPendingAsync());
    }

    [Fact]
    public async Task RequestStillInProgress_IsRetriedLater()
    {
        var server = new RecordingServer(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("{\"errorCodes\":[\"common.request_in_progress\"]}")
        });
        var (outbox, _) = await CreateAsync(server);

        await outbox.SubmitAsync(
            outbox.Create(OutboxKinds.CompleteFreeWorkout, HttpMethod.Post, "api/v1/workouts/free/complete", "{}"),
            TimeSpan.FromSeconds(5));

        Assert.Single(await outbox.GetPendingAsync());
    }

    [Fact]
    public async Task Rejection_RemovesOperation_TellsHandler_AndLetsTheRestThrough()
    {
        var server = new RecordingServer(request => request.RequestUri!.ToString().Contains("/1/")
            ? new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("{\"errorCodes\":[\"workout.already_completed\"]}") }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var (outbox, reachability) = await CreateAsync(server);
        var handler = new RecordingHandler();
        outbox.ResultHandler = handler;
        reachability.ReportServerUnavailable();
        await outbox.EnqueueAsync(outbox.Create(OutboxKinds.CompleteScheduledWorkout, HttpMethod.Post, "api/v1/workouts/days/1/complete", "{}", "Legs"));
        await outbox.EnqueueAsync(outbox.Create(OutboxKinds.UpdateExercise, HttpMethod.Put, "api/v1/exercises/7", "{}"));
        reachability.ReportServerResponded();

        Assert.Equal(2, await outbox.FlushAsync());

        Assert.Empty(await outbox.GetPendingAsync());
        var rejected = Assert.Single(handler.Rejected);
        Assert.Equal(409, rejected.Status);
        Assert.Equal("Legs", rejected.Operation.Label);
        Assert.Single(handler.Sent);
    }

    [Fact]
    public async Task NetworkError_StopsTheQueue_AndSurvivesRestart()
    {
        var server = new RecordingServer(_ => throw new HttpRequestException("offline"));
        var (outbox, _) = await CreateAsync(server);
        await outbox.EnqueueAsync(outbox.Create(OutboxKinds.UpdateExercise, HttpMethod.Put, "api/v1/exercises/1", "{}"));
        await outbox.EnqueueAsync(outbox.Create(OutboxKinds.UpdateExercise, HttpMethod.Put, "api/v1/exercises/2", "{}"));

        Assert.Equal(0, await outbox.FlushAsync());
        Assert.Single(server.Requests);

        var restarted = await CreateAsync(new RecordingServer(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }));
        Assert.Equal(2, (await restarted.Outbox.GetPendingAsync()).Count);
        Assert.Equal(2, await restarted.Outbox.FlushAsync());
    }

    [Fact]
    public async Task Issues_AreKeptNewestFirst_AndCanBeCleared()
    {
        var (outbox, _) = await CreateAsync(new RecordingServer(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        await outbox.AddIssueAsync(new SyncIssue(Guid.NewGuid(), OutboxKinds.UpdateExercise, "A", "first", DateTimeOffset.UtcNow));
        await outbox.AddIssueAsync(new SyncIssue(Guid.NewGuid(), OutboxKinds.UpdateExercise, "B", "second", DateTimeOffset.UtcNow));

        Assert.Equal(["second", "first"], (await outbox.GetIssuesAsync()).Select(x => x.Message));
        await outbox.ClearIssuesAsync();
        Assert.Empty(await outbox.GetIssuesAsync());
    }

    private async Task<(OutboxSync Outbox, ServerReachability Reachability)> CreateAsync(RecordingServer server)
    {
        var store = new OfflineDocumentStore(_root);
        await store.UseAccountAsync("outbox@example.test");
        var reachability = new ServerReachability(TimeProvider.System);
        var client = new HttpClient(server) { BaseAddress = new Uri("https://example.test/") };
        return (new OutboxSync(store, client, reachability, TimeProvider.System), reachability);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class RecordingServer(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Line, string? Key)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryGetValues(OutboxSync.IdempotencyHeader, out var keys);
            Requests.Add(($"{request.Method} {request.RequestUri!.PathAndQuery.TrimStart('/')}", keys?.Single()));
            return Task.FromResult(respond(request));
        }
    }

    private sealed class RecordingHandler : IOutboxResultHandler
    {
        public List<OutboxOperation> Sent { get; } = [];
        public List<(OutboxOperation Operation, int Status)> Rejected { get; } = [];

        public Task OnSentAsync(OutboxOperation operation, string body)
        {
            Sent.Add(operation);
            return Task.CompletedTask;
        }

        public Task OnRejectedAsync(OutboxOperation operation, int statusCode, string body)
        {
            Rejected.Add((operation, statusCode));
            return Task.CompletedTask;
        }
    }
}

public sealed class OfflineProjectionTests
{
    [Fact]
    public void ScheduledResults_UpdateSetsAndStatus_AndFeedHistory()
    {
        var plan = new TrainingPlanApiResponse(1, "Legs", new DateTime(2026, 9, 1),
        [
            new ExerciseApiResponse(10, "Squat", 1, "NotCompleted", 1, 3, false, [new ExerciseSetApiResponse(1, 8, 50, false)]),
            new ExerciseApiResponse(11, "Lunge", 1, "NotCompleted", 1, 4, false, [new ExerciseSetApiResponse(1, 8, 20, false)])
        ]);
        var request = new CompleteScheduledWorkoutRequest(
            new DateTime(2026, 9, 28, 19, 0, 0),
            [new CompletedExerciseRequest(10, "Hard", [new SaveExerciseSetRequest(2, 6, 65, true), new SaveExerciseSetRequest(1, 8, 60, true)])]);

        var completed = OfflineProjections.ApplyScheduledResults(plan, request);
        var history = OfflineProjections.ScheduledHistory(completed, request.CompletedAt, OfflineIds.Next());

        Assert.Equal("Hard", completed.Exercises[0].Status);
        Assert.Equal([60d, 65d], completed.Exercises[0].Sets.Select(x => x.Weight));
        Assert.Equal(2, completed.Exercises[0].SetsCount);
        Assert.Equal(20, completed.Exercises[1].Sets.Single().Weight);
        Assert.True(OfflineIds.IsLocal(history.Id));
        Assert.Equal(request.CompletedAt, history.Date);
        Assert.Equal(["Squat", "Lunge"], history.Exercises.Select(x => x.Name));
    }

    [Fact]
    public void FreeWorkoutTemplate_DropsCompletionMarks()
    {
        var request = new CompleteFreeWorkoutRequest(
            true,
            "  Pull day ",
            [new SaveExerciseRequest("Row", 1, "Hard", 5, [new SaveExerciseSetRequest(1, 10, 40, true)])],
            new DateTime(2026, 9, 28, 20, 0, 0));

        var template = OfflineProjections.TemplateFromFreeWorkout(request, OfflineIds.Next(), request.CompletedAt!.Value);
        var history = OfflineProjections.FreeHistory(request, "Pull day", request.CompletedAt.Value, OfflineIds.Next());

        Assert.Equal("Pull day", template.WorkoutName);
        Assert.False(template.Exercises.Single().Sets.Single().Completed);
        Assert.True(OfflineIds.IsLocal(template.Exercises.Single().Id));
        Assert.True(history.Exercises.Single().Sets.Single().Completed);
    }

    [Fact]
    public void ReorderSuperset_SwapsMembersInTheirSlots()
    {
        var plan = new TrainingPlanApiResponse(1, "Arms", DateTime.Today,
        [
            new ExerciseApiResponse(1, "A", 1, "NotCompleted", 1, null, false, [], 7),
            new ExerciseApiResponse(2, "Solo", 1, "NotCompleted", 1, null, false, []),
            new ExerciseApiResponse(3, "B", 1, "NotCompleted", 1, null, false, [], 7)
        ]);

        var reordered = OfflineProjections.ReorderSuperset(plan, [3, 1]);

        Assert.Equal([3, 2, 1], reordered.Exercises.Select(x => x.Id));
        Assert.Same(plan, OfflineProjections.ReorderSuperset(plan, [3, 99]));
    }

    [Fact]
    public void LocalIds_DoNotCollideWithServerOrDraftIds()
    {
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var id = OfflineIds.Next();
            Assert.True(OfflineIds.IsLocal(id));
            Assert.True(id < -1_000_000);
        }

        Assert.False(OfflineIds.IsLocal(-1));
        Assert.False(OfflineIds.IsLocal(42));
    }
}
