using System.Text.Json;
using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Localization;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Планы и упражнения: с сервера, а без связи — из сохранённой копии.
/// </summary>
/// <remarks>
/// Все правки идут через офлайн-очередь; созданное без связи получает временный
/// ID, который очередь заменит настоящим. Исключение — черновик свободной
/// тренировки: без связи его упражнения правятся только на телефоне, потому что
/// завершение всё равно отправит на сервер полный состав.
/// </remarks>
public sealed class OfflineWorkoutApiClient(WorkoutApiClient inner, OfflineRuntime runtime, TimeProvider timeProvider, IAppLanguageService language) : IWorkoutApiClient
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
        var resolved = await runtime.Outbox.ResolveAsync(id, cancellationToken);

        // План, созданный без связи, сервер ещё не знает.
        if (OfflineIds.IsLocal(resolved))
        {
            return (await LoadPlansAsync(cancellationToken)).Find(resolved) is { } localPlan
                ? ApiResult<TrainingPlanApiResponse>.Success(localPlan)
                : ApiResult<TrainingPlanApiResponse>.Failure(ApiErrorMessages.Get(
                    WorkoutPlanner.Localization.ApiErrorCodes.NotFound));
        }

        return await runtime.ReadAsync(
            token => inner.GetPlanAsync(resolved, token),
            async () =>
            {
                var plans = await LoadPlansAsync(cancellationToken);
                return plans.Find(resolved) ?? plans.Find(id);
            },
            plan => UpdatePlansAsync(x => x.Upsert(plan, listed: x.Order.Contains(plan.Id)), cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult<TrainingPlanApiResponse>> CreatePlanAsync(CreateTrainingPlanRequest request, CancellationToken cancellationToken = default)
    {
        var localId = OfflineIds.Next();
        var operation = runtime.Outbox.Create(
            OutboxKinds.CreatePlan,
            HttpMethod.Post,
            "api/v1/training-plans",
            JsonSerializer.Serialize(request, Json),
            request.WorkoutName.Trim(),
            localId);
        return await runtime.SubmitAsync(
            operation,
            async () =>
            {
                var plan = new TrainingPlanApiResponse(localId, request.WorkoutName.Trim(), timeProvider.GetLocalNow().Date, []);
                await UpdatePlansAsync(x => x.Upsert(plan), cancellationToken);
                return plan;
            },
            body => AcceptPlanAsync(body, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult<TrainingPlanApiResponse>> RenamePlanAsync(int id, RenameTrainingPlanRequest request, CancellationToken cancellationToken = default)
    {
        id = await runtime.Outbox.ResolveAsync(id, cancellationToken);
        var operation = runtime.Outbox.Create(
            OutboxKinds.RenamePlan,
            HttpMethod.Put,
            $"api/v1/training-plans/{id}",
            JsonSerializer.Serialize(request, Json),
            request.WorkoutName.Trim());
        return await runtime.SubmitAsync(
            operation,
            async () =>
            {
                var current = (await LoadPlansAsync(cancellationToken)).Find(id)
                    ?? new TrainingPlanApiResponse(id, string.Empty, timeProvider.GetLocalNow().Date, []);
                var renamed = current with { WorkoutName = request.WorkoutName.Trim() };
                await UpdatePlansAsync(x => x.Upsert(renamed, listed: x.Order.Contains(id)), cancellationToken);
                return renamed;
            },
            body => AcceptPlanAsync(body, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult> DeletePlanAsync(int id, CancellationToken cancellationToken = default)
    {
        id = await runtime.Outbox.ResolveAsync(id, cancellationToken);
        var name = (await LoadPlansAsync(cancellationToken)).Find(id)?.WorkoutName;
        var operation = runtime.Outbox.Create(
            OutboxKinds.DeletePlan,
            HttpMethod.Delete,
            $"api/v1/training-plans/{id}",
            body: null,
            name);
        return await runtime.SubmitAsync(
            operation,
            async () =>
            {
                // Сервер удаляет вместе с планом и его дни в календаре.
                await UpdatePlansAsync(x => x.Remove(id), cancellationToken);
                await runtime.UpdateCopyAsync<OfflineCalendar>(
                    OfflineKeys.Calendar,
                    x => (x ?? OfflineCalendar.Empty).RemovePlan(id),
                    cancellationToken);
            },
            cancellationToken,
            notFoundIsSuccess: true);
    }

    public async Task<ApiResult<TrainingPlanApiResponse>> ReorderSupersetAsync(int planId, int supersetGroupId, ReorderSupersetRequest request, CancellationToken cancellationToken = default)
    {
        planId = await runtime.Outbox.ResolveAsync(planId, cancellationToken);
        var exerciseIds = new List<int>();
        foreach (var exerciseId in request.ExerciseIds)
            exerciseIds.Add(await runtime.Outbox.ResolveAsync(exerciseId, cancellationToken));
        request = new ReorderSupersetRequest(exerciseIds);

        async Task<TrainingPlanApiResponse> ProjectAsync()
        {
            // Экран переставляет упражнения сам; копия плана нужна только для офлайн-чтения.
            var plan = (await LoadPlansAsync(cancellationToken)).Find(planId)
                ?? new TrainingPlanApiResponse(planId, string.Empty, timeProvider.GetLocalNow().Date, []);
            var reordered = OfflineProjections.ReorderSuperset(plan, request.ExerciseIds);
            await UpdatePlansAsync(x => x.Upsert(reordered, listed: x.Order.Contains(planId)), cancellationToken);
            return reordered;
        }

        if (await KeepsOnPhoneAsync(planId, exerciseId: null, cancellationToken))
            return ApiResult<TrainingPlanApiResponse>.Success(await ProjectAsync());

        var operation = runtime.Outbox.Create(
            OutboxKinds.ReorderSuperset,
            HttpMethod.Put,
            $"api/v1/training-plans/{planId}/supersets/{supersetGroupId}/order",
            JsonSerializer.Serialize(request, Json));
        return await runtime.SubmitAsync(
            operation,
            ProjectAsync,
            body => AcceptPlanAsync(body, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult<TrainingPlanApiResponse>> UpdateRestTimersAsync(int planId, UpdateRestTimersRequest request, CancellationToken cancellationToken = default)
    {
        planId = await runtime.Outbox.ResolveAsync(planId, cancellationToken);
        var exercises = new List<ExerciseRestTimerRequest>();
        foreach (var exercise in request.Exercises ?? [])
            exercises.Add(exercise with { ExerciseId = await runtime.Outbox.ResolveAsync(exercise.ExerciseId, cancellationToken) });
        var sets = new List<SetRestTimerRequest>();
        foreach (var set in request.Sets ?? [])
            sets.Add(set with { ExerciseId = await runtime.Outbox.ResolveAsync(set.ExerciseId, cancellationToken) });
        var afterExercises = new List<ExerciseRestAfterRequest>();
        foreach (var exercise in request.AfterExercises ?? [])
            afterExercises.Add(exercise with { ExerciseId = await runtime.Outbox.ResolveAsync(exercise.ExerciseId, cancellationToken) });
        request = request with { Exercises = exercises, Sets = sets, AfterExercises = afterExercises };
        var plans = await LoadPlansAsync(cancellationToken);

        async Task<TrainingPlanApiResponse> ProjectAsync()
        {
            var plan = (await LoadPlansAsync(cancellationToken)).Find(planId)
                ?? new TrainingPlanApiResponse(planId, string.Empty, timeProvider.GetLocalNow().Date, []);
            var updated = OfflineProjections.ApplyRestTimers(plan, request);
            await UpdatePlansAsync(x => x.Upsert(updated, listed: x.Order.Contains(planId)), cancellationToken);
            return updated;
        }

        // Упражнения свободной тренировки, добавленные без связи, сервер не знает:
        // их таймеры уедут вместе с завершением тренировки.
        if (await KeepsOnPhoneAsync(planId, exerciseId: null, cancellationToken) ||
            (await IsFreeDraftPlanAsync(planId, cancellationToken) &&
             exercises.Select(x => x.ExerciseId)
                 .Concat(sets.Select(x => x.ExerciseId))
                 .Concat(afterExercises.Select(x => x.ExerciseId))
                 .Any(OfflineIds.IsLocal)))
        {
            return ApiResult<TrainingPlanApiResponse>.Success(await ProjectAsync());
        }

        var operation = runtime.Outbox.Create(
            OutboxKinds.UpdateRestTimers,
            HttpMethod.Put,
            $"api/v1/training-plans/{planId}/rest-timers",
            JsonSerializer.Serialize(request, Json),
            plans.Find(planId)?.WorkoutName);
        return await runtime.SubmitAsync(
            operation,
            ProjectAsync,
            body => AcceptPlanAsync(body, cancellationToken),
            cancellationToken);
    }

    public Task<ApiResult<IReadOnlyList<ExerciseDefinitionApiResponse>>> GetExerciseDefinitionsAsync(CancellationToken cancellationToken = default) =>
        runtime.ReadAsync(
            inner.GetExerciseDefinitionsAsync,
            async () => await runtime.Store.GetAsync<List<ExerciseDefinitionApiResponse>>(
                OfflineKeys.ExerciseDefinitionsIn(language.Current),
                cancellationToken) as IReadOnlyList<ExerciseDefinitionApiResponse>,
            definitions => runtime.Store.SetAsync(
                OfflineKeys.ExerciseDefinitionsIn(language.Current),
                definitions.ToList(),
                cancellationToken),
            cancellationToken);

    public async Task<ApiResult<ExerciseApiResponse>> CreateExerciseAsync(int planId, SaveExerciseRequest request, CancellationToken cancellationToken = default)
    {
        planId = await runtime.Outbox.ResolveAsync(planId, cancellationToken);
        if (await IsFreeDraftPlanAsync(planId, cancellationToken))
            return await CreateDraftExerciseAsync(planId, request, cancellationToken);

        var localId = OfflineIds.Next();
        var operation = runtime.Outbox.Create(
            OutboxKinds.CreateExercise,
            HttpMethod.Post,
            $"api/v1/training-plans/{planId}/exercises",
            JsonSerializer.Serialize(request, Json),
            request.Name.Trim(),
            localId);
        return await runtime.SubmitAsync(
            operation,
            async () =>
            {
                var exercise = OfflineProjections.Exercise(localId, planId, request, hasPhoto: false);
                await UpdatePlansAsync(x => x.UpsertExercise(exercise), cancellationToken);
                return exercise;
            },
            body => AcceptExerciseAsync(body, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult<ExerciseApiResponse>> UpdateExerciseAsync(int id, SaveExerciseRequest request, CancellationToken cancellationToken = default)
    {
        var originalId = id;
        id = await runtime.Outbox.ResolveAsync(id, cancellationToken);
        var plans = await LoadPlansAsync(cancellationToken);
        var current = plans.FindExercise(id) ?? plans.FindExercise(originalId);

        async Task<ExerciseApiResponse> ProjectAsync()
        {
            var exercise = OfflineProjections.Exercise(
                id,
                current?.TrainingPlanId ?? 0,
                request,
                current?.HasPhoto ?? false,
                current);
            await UpdatePlansAsync(x => x.UpsertExercise(exercise), cancellationToken);
            return exercise;
        }

        if (current is not null && await KeepsOnPhoneAsync(current.TrainingPlanId, id, cancellationToken))
            return ApiResult<ExerciseApiResponse>.Success(await ProjectAsync());

        var operation = runtime.Outbox.Create(
            OutboxKinds.UpdateExercise,
            HttpMethod.Put,
            $"api/v1/exercises/{id}",
            JsonSerializer.Serialize(request, Json),
            request.Name);
        return await runtime.SubmitAsync(
            operation,
            ProjectAsync,
            body => AcceptExerciseAsync(body, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult> DeleteExerciseAsync(int id, CancellationToken cancellationToken = default)
    {
        id = await runtime.Outbox.ResolveAsync(id, cancellationToken);
        var current = (await LoadPlansAsync(cancellationToken)).FindExercise(id);
        Task ForgetAsync() => UpdatePlansAsync(x => x.RemoveExercise(id), cancellationToken);

        if (current is not null && await KeepsOnPhoneAsync(current.TrainingPlanId, id, cancellationToken))
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
    /// Упражнение свободной тренировки. Со связью оно сразу уходит на сервер,
    /// чтобы его видели часы; без связи — остаётся на телефоне до завершения.
    /// </summary>
    private async Task<ApiResult<ExerciseApiResponse>> CreateDraftExerciseAsync(
        int planId,
        SaveExerciseRequest request,
        CancellationToken cancellationToken)
    {
        if (!await KeepsOnPhoneAsync(planId, exerciseId: null, cancellationToken))
        {
            using var scope = TransportScope.Begin();
            var result = await runtime.WriteAsync(
                token => inner.CreateExerciseAsync(planId, request, token),
                exercise => UpdatePlansAsync(x => x.UpsertExercise(exercise), cancellationToken),
                cancellationToken);
            if (result.Succeeded || !scope.ServerUnavailable)
                return result;
        }

        await EnsureDraftPlanOnPhoneAsync(planId, cancellationToken);
        var created = OfflineProjections.Exercise(OfflineIds.Next(), planId, request, hasPhoto: false);
        await UpdatePlansAsync(x => x.UpsertExercise(created), cancellationToken);
        return ApiResult<ExerciseApiResponse>.Success(created);
    }

    /// <summary>
    /// Правки остаются на телефоне только у свободной тренировки: когда нет связи,
    /// когда сам черновик начат без связи или упражнение добавлено без неё —
    /// такого упражнения сервер не знает, а завершение отправит полный состав.
    /// </summary>
    private async Task<bool> KeepsOnPhoneAsync(int planId, int? exerciseId, CancellationToken cancellationToken) =>
        await IsFreeDraftPlanAsync(planId, cancellationToken) &&
        (runtime.Reachability.IsOffline ||
         OfflineIds.IsLocal(planId) ||
         exerciseId is { } id && OfflineIds.IsLocal(id));

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

    private async Task<TrainingPlanApiResponse> AcceptPlanAsync(string body, CancellationToken cancellationToken)
    {
        var plan = JsonSerializer.Deserialize<TrainingPlanApiResponse>(body, Json)
            ?? throw new JsonException("Empty plan response.");
        await UpdatePlansAsync(x => x.Upsert(plan, listed: x.Order.Contains(plan.Id) || !x.Plans.ContainsKey(plan.Id)), cancellationToken);
        return plan;
    }

    private async Task<ExerciseApiResponse> AcceptExerciseAsync(string body, CancellationToken cancellationToken)
    {
        var exercise = JsonSerializer.Deserialize<ExerciseApiResponse>(body, Json)
            ?? throw new JsonException("Empty exercise response.");
        await UpdatePlansAsync(x => x.UpsertExercise(exercise), cancellationToken);
        return exercise;
    }

    private async Task<OfflinePlans> LoadPlansAsync(CancellationToken cancellationToken) =>
        await runtime.Store.GetAsync<OfflinePlans>(OfflineKeys.Plans, cancellationToken) ?? OfflinePlans.Empty;

    private Task UpdatePlansAsync(Func<OfflinePlans, OfflinePlans> update, CancellationToken cancellationToken) =>
        runtime.UpdateCopyAsync<OfflinePlans>(
            OfflineKeys.Plans,
            current => update(current ?? OfflinePlans.Empty),
            cancellationToken);
}
