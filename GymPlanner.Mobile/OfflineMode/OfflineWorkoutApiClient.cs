using System.Text.Json;
using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Планы и упражнения: с сервера, а без связи — из сохранённой копии.
/// </summary>
/// <remarks>
/// Правки упражнений идут через офлайн-очередь. Свободная тренировка без связи
/// живёт только на телефоне: её упражнения правятся локально, потому что
/// завершение всё равно отправит на сервер полный состав.
/// </remarks>
public sealed class OfflineWorkoutApiClient(WorkoutApiClient inner, OfflineRuntime runtime, TimeProvider timeProvider) : IWorkoutApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ApiResult<IReadOnlyList<TrainingPlanApiResponse>>> GetPlansAsync(CancellationToken cancellationToken = default) =>
        runtime.ReadAsync(
            inner.GetPlansAsync,
            async () => await runtime.Store.GetAsync<OfflinePlans>(OfflineKeys.Plans, cancellationToken) is { } plans
                ? plans.Listed
                : null,
            plans => runtime.Store.UpdateAsync<OfflinePlans>(
                OfflineKeys.Plans,
                current => (current ?? OfflinePlans.Empty).WithListed(plans),
                cancellationToken),
            cancellationToken);

    public async Task<ApiResult<TrainingPlanApiResponse>> GetPlanAsync(int id, CancellationToken cancellationToken = default)
    {
        // План, созданный без связи, сервер ещё не знает.
        if (OfflineIds.IsLocal(id))
        {
            return (await LoadPlansAsync(cancellationToken)).Find(id) is { } localPlan
                ? ApiResult<TrainingPlanApiResponse>.Success(localPlan)
                : ApiResult<TrainingPlanApiResponse>.Failure(Localization.ApiErrorMessages.Get(
                    WorkoutPlanner.Localization.ApiErrorCodes.NotFound));
        }

        return await runtime.ReadAsync(
            token => inner.GetPlanAsync(id, token),
            async () => (await LoadPlansAsync(cancellationToken)).Find(id),
            plan => UpdatePlansAsync(x => x.Upsert(plan, listed: x.Order.Contains(plan.Id)), cancellationToken),
            cancellationToken);
    }

    public Task<ApiResult<TrainingPlanApiResponse>> CreatePlanAsync(CreateTrainingPlanRequest request, CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            token => inner.CreatePlanAsync(request, token),
            plan => UpdatePlansAsync(x => x.Upsert(plan), cancellationToken),
            cancellationToken);

    public Task<ApiResult<TrainingPlanApiResponse>> RenamePlanAsync(int id, RenameTrainingPlanRequest request, CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            token => inner.RenamePlanAsync(id, request, token),
            plan => UpdatePlansAsync(x => x.Upsert(plan, listed: x.Order.Contains(plan.Id)), cancellationToken),
            cancellationToken);

    public Task<ApiResult> DeletePlanAsync(int id, CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            token => inner.DeletePlanAsync(id, token),
            async () =>
            {
                await UpdatePlansAsync(x => x.Remove(id), cancellationToken);
                await runtime.Store.UpdateAsync<OfflineCalendar>(
                    OfflineKeys.Calendar,
                    x => (x ?? OfflineCalendar.Empty).RemovePlan(id),
                    cancellationToken);
            },
            cancellationToken);

    public async Task<ApiResult<TrainingPlanApiResponse>> ReorderSupersetAsync(int planId, int supersetGroupId, ReorderSupersetRequest request, CancellationToken cancellationToken = default)
    {
        async Task<TrainingPlanApiResponse> ProjectAsync()
        {
            // Экран переставляет упражнения сам; копия плана нужна только для офлайн-чтения.
            var plan = (await LoadPlansAsync(cancellationToken)).Find(planId)
                ?? new TrainingPlanApiResponse(planId, string.Empty, timeProvider.GetLocalNow().Date, []);
            var reordered = OfflineProjections.ReorderSuperset(plan, request.ExerciseIds);
            await UpdatePlansAsync(x => x.Upsert(reordered, listed: x.Order.Contains(planId)), cancellationToken);
            return reordered;
        }

        if (await KeepsOnPhoneAsync(planId, cancellationToken))
            return ApiResult<TrainingPlanApiResponse>.Success(await ProjectAsync());

        var operation = runtime.Outbox.Create(
            OutboxKinds.ReorderSuperset,
            HttpMethod.Put,
            $"api/v1/training-plans/{planId}/supersets/{supersetGroupId}/order",
            JsonSerializer.Serialize(request, Json));
        return await runtime.SubmitAsync(
            operation,
            ProjectAsync,
            async body =>
            {
                var plan = JsonSerializer.Deserialize<TrainingPlanApiResponse>(body, Json)
                    ?? throw new JsonException("Empty plan response.");
                await UpdatePlansAsync(x => x.Upsert(plan, listed: x.Order.Contains(plan.Id)), cancellationToken);
                return plan;
            },
            cancellationToken);
    }

    public Task<ApiResult<IReadOnlyList<ExerciseDefinitionApiResponse>>> GetExerciseDefinitionsAsync(CancellationToken cancellationToken = default) =>
        runtime.ReadAsync(
            inner.GetExerciseDefinitionsAsync,
            async () => await runtime.Store.GetAsync<List<ExerciseDefinitionApiResponse>>(
                OfflineKeys.ExerciseDefinitions,
                cancellationToken) as IReadOnlyList<ExerciseDefinitionApiResponse>,
            definitions => runtime.Store.SetAsync(
                OfflineKeys.ExerciseDefinitions,
                definitions.ToList(),
                cancellationToken),
            cancellationToken);

    public async Task<ApiResult<ExerciseApiResponse>> CreateExerciseAsync(int planId, SaveExerciseRequest request, CancellationToken cancellationToken = default)
    {
        if (!await KeepsOnPhoneAsync(planId, cancellationToken))
        {
            using var scope = TransportScope.Begin();
            var result = await runtime.WriteAsync(
                token => inner.CreateExerciseAsync(planId, request, token),
                exercise => UpdatePlansAsync(x => x.UpsertExercise(exercise), cancellationToken),
                cancellationToken);
            // Связь пропала посреди свободной тренировки — упражнение остаётся на телефоне.
            if (result.Succeeded || !scope.ServerUnavailable || !await IsFreeDraftPlanAsync(planId, cancellationToken))
                return result;
        }

        await EnsureDraftPlanOnPhoneAsync(planId, cancellationToken);
        var created = OfflineProjections.Exercise(OfflineIds.Next(), planId, request, hasPhoto: false);
        await UpdatePlansAsync(x => x.UpsertExercise(created), cancellationToken);
        return ApiResult<ExerciseApiResponse>.Success(created);
    }

    public async Task<ApiResult<ExerciseApiResponse>> UpdateExerciseAsync(int id, SaveExerciseRequest request, CancellationToken cancellationToken = default)
    {
        var current = (await LoadPlansAsync(cancellationToken)).FindExercise(id);

        async Task<ExerciseApiResponse> ProjectAsync()
        {
            var exercise = OfflineProjections.Exercise(id, current?.TrainingPlanId ?? 0, request, current?.HasPhoto ?? false);
            await UpdatePlansAsync(x => x.UpsertExercise(exercise), cancellationToken);
            return exercise;
        }

        if (OfflineIds.IsLocal(id) ||
            current is not null && await KeepsOnPhoneAsync(current.TrainingPlanId, cancellationToken))
        {
            return ApiResult<ExerciseApiResponse>.Success(await ProjectAsync());
        }

        var operation = runtime.Outbox.Create(
            OutboxKinds.UpdateExercise,
            HttpMethod.Put,
            $"api/v1/exercises/{id}",
            JsonSerializer.Serialize(request, Json),
            request.Name);
        return await runtime.SubmitAsync(
            operation,
            ProjectAsync,
            async body =>
            {
                var exercise = JsonSerializer.Deserialize<ExerciseApiResponse>(body, Json)
                    ?? throw new JsonException("Empty exercise response.");
                await UpdatePlansAsync(x => x.UpsertExercise(exercise), cancellationToken);
                return exercise;
            },
            cancellationToken);
    }

    public async Task<ApiResult> DeleteExerciseAsync(int id, CancellationToken cancellationToken = default)
    {
        var current = (await LoadPlansAsync(cancellationToken)).FindExercise(id);
        Task ForgetAsync() => UpdatePlansAsync(x => x.RemoveExercise(id), cancellationToken);

        if (OfflineIds.IsLocal(id) ||
            current is not null && await KeepsOnPhoneAsync(current.TrainingPlanId, cancellationToken))
        {
            await ForgetAsync();
            return ApiResult.Success;
        }

        var operation = runtime.Outbox.Create(
            OutboxKinds.DeleteExercise,
            HttpMethod.Delete,
            $"api/v1/exercises/{id}",
            body: null,
            current?.Name);
        return await runtime.SubmitAsync(operation, ForgetAsync, cancellationToken, notFoundIsSuccess: true);
    }

    /// <summary>
    /// Правки этого плана остаются на телефоне: план создан без связи или это
    /// черновик свободной тренировки, а связи сейчас нет.
    /// </summary>
    private async Task<bool> KeepsOnPhoneAsync(int planId, CancellationToken cancellationToken) =>
        OfflineIds.IsLocal(planId) ||
        runtime.Reachability.IsOffline && await IsFreeDraftPlanAsync(planId, cancellationToken);

    private async Task<bool> IsFreeDraftPlanAsync(int planId, CancellationToken cancellationToken) =>
        (await runtime.Store.GetAsync<OfflineOptional<FreeWorkoutDraftResponse>>(
            OfflineKeys.FreeWorkoutDraft,
            cancellationToken))?.Value?.TrainingPlanId == planId;

    // Черновик, начатый на сервере, мог ещё не попасть в копию на телефоне.
    private async Task EnsureDraftPlanOnPhoneAsync(int planId, CancellationToken cancellationToken)
    {
        if ((await LoadPlansAsync(cancellationToken)).Find(planId) is not null)
            return;

        var draft = (await runtime.Store.GetAsync<OfflineOptional<FreeWorkoutDraftResponse>>(
            OfflineKeys.FreeWorkoutDraft,
            cancellationToken))?.Value;
        var plan = new TrainingPlanApiResponse(
            planId,
            draft?.WorkoutName ?? string.Empty,
            timeProvider.GetLocalNow().Date,
            []);
        await UpdatePlansAsync(x => x.Upsert(plan, listed: false), cancellationToken);
    }

    private async Task<OfflinePlans> LoadPlansAsync(CancellationToken cancellationToken) =>
        await runtime.Store.GetAsync<OfflinePlans>(OfflineKeys.Plans, cancellationToken) ?? OfflinePlans.Empty;

    private Task UpdatePlansAsync(Func<OfflinePlans, OfflinePlans> update, CancellationToken cancellationToken) =>
        runtime.UpdateCopyAsync<OfflinePlans>(
            OfflineKeys.Plans,
            current => update(current ?? OfflinePlans.Empty),
            cancellationToken);
}
