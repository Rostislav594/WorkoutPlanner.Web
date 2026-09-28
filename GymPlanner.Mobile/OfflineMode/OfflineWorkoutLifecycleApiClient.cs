using System.Text.Json;
using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Localization;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>Календарь, сегодняшняя тренировка и история с офлайн-копией.</summary>
public sealed class OfflineWorkoutLifecycleApiClient(
    WorkoutLifecycleApiClient inner,
    OfflineRuntime runtime,
    TimeProvider timeProvider) : IWorkoutLifecycleApiClient
{
    public Task<ApiResult<IReadOnlyList<WorkoutDayApiResponse>>> GetCalendarAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default) =>
        runtime.ReadAsync(
            token => inner.GetCalendarAsync(from, to, token),
            async () => await runtime.Store.GetAsync<OfflineCalendar>(OfflineKeys.Calendar, cancellationToken) is { } calendar
                ? calendar.InRange(from, to)
                : null,
            days => UpdateCalendarAsync(x => x.WithRange(from, to, days), cancellationToken),
            cancellationToken);

    /// <summary>Назначение тренировки на дату; без связи день получает временный ID.</summary>
    public async Task<ApiResult<WorkoutDayApiResponse>> ScheduleWorkoutAsync(
        ScheduleWorkoutRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with
        {
            TrainingPlanId = await runtime.Outbox.ResolveAsync(request.TrainingPlanId, cancellationToken)
        };
        var localId = OfflineIds.Next();
        var planName = (await runtime.Store.GetAsync<OfflinePlans>(OfflineKeys.Plans, cancellationToken))?
            .Find(request.TrainingPlanId)?.WorkoutName;
        var operation = runtime.Outbox.Create(
            OutboxKinds.ScheduleWorkout,
            HttpMethod.Post,
            "api/v1/calendar",
            JsonSerializer.Serialize(request, Json),
            planName,
            localId);
        return await runtime.SubmitAsync(
            operation,
            async () =>
            {
                var day = new WorkoutDayApiResponse(localId, request.Date.Date, request.TrainingPlanId, false);
                await UpdateCalendarAsync(x => x.Upsert(day), cancellationToken);
                return day;
            },
            body => AcceptDayAsync(body, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult> DeleteCalendarDayAsync(int id, CancellationToken cancellationToken = default)
    {
        id = await runtime.Outbox.ResolveAsync(id, cancellationToken);
        var operation = runtime.Outbox.Create(
            OutboxKinds.DeleteWorkoutDay,
            HttpMethod.Delete,
            $"api/v1/calendar/{id}",
            body: null,
            await PlanNameForDayAsync(id, cancellationToken));
        return await runtime.SubmitAsync(
            operation,
            () => UpdateCalendarAsync(x => x.Remove(id), cancellationToken),
            cancellationToken,
            notFoundIsSuccess: true);
    }

    public async Task<ApiResult<WorkoutDayApiResponse>> MoveCalendarDayAsync(
        int id,
        MoveWorkoutRequest request,
        CancellationToken cancellationToken = default)
    {
        id = await runtime.Outbox.ResolveAsync(id, cancellationToken);
        var calendar = await runtime.Store.GetAsync<OfflineCalendar>(OfflineKeys.Calendar, cancellationToken);
        var current = calendar?.Days.GetValueOrDefault(id);
        var operation = runtime.Outbox.Create(
            OutboxKinds.MoveWorkout,
            HttpMethod.Put,
            $"api/v1/calendar/{id}/date",
            JsonSerializer.Serialize(request, Json),
            await PlanNameForDayAsync(id, cancellationToken));
        return await runtime.SubmitAsync(
            operation,
            async () =>
            {
                var moved = (current ?? new WorkoutDayApiResponse(id, request.Date.Date, 0, false)) with
                {
                    Date = request.Date.Date
                };
                await UpdateCalendarAsync(x => x.Upsert(moved), cancellationToken);
                return moved;
            },
            body => AcceptDayAsync(body, cancellationToken),
            cancellationToken);
    }

    private async Task<WorkoutDayApiResponse> AcceptDayAsync(string body, CancellationToken cancellationToken)
    {
        var day = JsonSerializer.Deserialize<WorkoutDayApiResponse>(body, Json)
            ?? throw new JsonException("Empty calendar day response.");
        await UpdateCalendarAsync(x => x.Upsert(day), cancellationToken);
        return day;
    }

    private async Task<string?> PlanNameForDayAsync(int dayId, CancellationToken cancellationToken)
    {
        var day = (await runtime.Store.GetAsync<OfflineCalendar>(OfflineKeys.Calendar, cancellationToken))?
            .Days.GetValueOrDefault(dayId);
        return day is null
            ? null
            : (await runtime.Store.GetAsync<OfflinePlans>(OfflineKeys.Plans, cancellationToken))?
                .Find(day.TrainingPlanId)?.WorkoutName;
    }

    /// <summary>
    /// Сегодняшняя тренировка. Без связи собирается из сохранённых календаря и
    /// планов по дате телефона.
    /// </summary>
    public Task<OptionalApiResult<TodayWorkoutApiResponse>> StartTodayWorkoutAsync(
        CancellationToken cancellationToken = default) =>
        runtime.ReadOptionalAsync(
            inner.StartTodayWorkoutAsync,
            async () =>
            {
                var calendar = await runtime.Store.GetAsync<OfflineCalendar>(OfflineKeys.Calendar, cancellationToken);
                var plans = await runtime.Store.GetAsync<OfflinePlans>(OfflineKeys.Plans, cancellationToken);
                if (calendar is null || plans is null)
                    return (false, null);

                var today = timeProvider.GetLocalNow().DateTime;
                return (true, TodayWorkoutResolver.Resolve(calendar, plans, today));
            },
            async today =>
            {
                if (today is null)
                    return;

                await UpdateCalendarAsync(x => x.Upsert(today.Day), cancellationToken);
                await runtime.Store.UpdateAsync<OfflinePlans>(
                    OfflineKeys.Plans,
                    x => (x ?? OfflinePlans.Empty).Upsert(
                        today.TrainingPlan,
                        listed: x?.Order.Contains(today.TrainingPlan.Id) == true),
                    cancellationToken);
            },
            cancellationToken);

    public Task<ApiResult<WorkoutHistoryApiResponse>> CompleteTodayWorkoutAsync(
        CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            inner.CompleteTodayWorkoutAsync,
            async history =>
            {
                var today = timeProvider.GetLocalNow().Date;
                await UpdateCalendarAsync(
                    x => x.Days.Values
                        .Where(day => day.Date.Date == today && !day.IsCompleted)
                        .Aggregate(x, (calendar, day) => calendar.Upsert(day with { IsCompleted = true })),
                    cancellationToken);
                await AddHistoryAsync(history, cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Завершение дня через офлайн-очередь. Без связи тренировка сразу видна
    /// завершённой: в календаре, в истории и с новыми весами в плане.
    /// </summary>
    public async Task<ApiResult<WorkoutHistoryApiResponse>> CompleteScheduledWorkoutAsync(
        int workoutDayId,
        CompleteScheduledWorkoutRequest request,
        CancellationToken cancellationToken = default)
    {
        workoutDayId = await runtime.Outbox.ResolveAsync(workoutDayId, cancellationToken);
        var calendar = await runtime.Store.GetAsync<OfflineCalendar>(OfflineKeys.Calendar, cancellationToken);
        var day = calendar?.Days.GetValueOrDefault(workoutDayId);
        var plans = await runtime.Store.GetAsync<OfflinePlans>(OfflineKeys.Plans, cancellationToken) ?? OfflinePlans.Empty;
        var plan = day is null ? null : plans.Find(day.TrainingPlanId);
        var historyId = OfflineIds.Next();
        var operation = runtime.Outbox.Create(
            OutboxKinds.CompleteScheduledWorkout,
            HttpMethod.Post,
            $"api/v1/workouts/days/{workoutDayId}/complete",
            JsonSerializer.Serialize(request, Json),
            plan?.WorkoutName,
            historyId);

        async Task<WorkoutHistoryApiResponse> RecordAsync(WorkoutHistoryApiResponse? serverHistory)
        {
            var completedPlan = plan is null ? null : OfflineProjections.ApplyScheduledResults(plan, request);
            if (completedPlan is not null)
            {
                await UpdatePlansAsync(
                    x => x.Upsert(completedPlan, listed: x.Order.Contains(completedPlan.Id)),
                    cancellationToken);
            }

            if (day is not null)
                await UpdateCalendarAsync(x => x.Upsert(day with { IsCompleted = true }), cancellationToken);

            var history = serverHistory ?? (completedPlan is null
                ? new WorkoutHistoryApiResponse(historyId, string.Empty, request.CompletedAt, string.Empty, false, [])
                : OfflineProjections.ScheduledHistory(completedPlan, request.CompletedAt, historyId));
            await AddHistoryAsync(history, cancellationToken);
            return history;
        }

        return await runtime.SubmitAsync(
            operation,
            () => RecordAsync(null),
            body => RecordAsync(JsonSerializer.Deserialize<WorkoutHistoryApiResponse>(body, Json)
                ?? throw new JsonException("Empty history response.")),
            cancellationToken);
    }

    /// <summary>
    /// Завершение свободной тренировки через офлайн-очередь. Время окончания
    /// ставит телефон: запрос может дойти до сервера и на следующий день.
    /// </summary>
    public async Task<ApiResult<CompleteFreeWorkoutResponse>> CompleteFreeWorkoutAsync(
        CompleteFreeWorkoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var completedAt = request.CompletedAt ?? timeProvider.GetLocalNow().DateTime;
        var stamped = request with { CompletedAt = completedAt };
        var workoutName = request.SaveAsTemplate && !string.IsNullOrWhiteSpace(request.TemplateName)
            ? request.TemplateName.Trim()
            : AppTexts.Get("Today_FreeWorkout");
        var historyId = OfflineIds.Next();
        var operation = runtime.Outbox.Create(
            OutboxKinds.CompleteFreeWorkout,
            HttpMethod.Post,
            "api/v1/workouts/free/complete",
            JsonSerializer.Serialize(stamped, Json),
            workoutName,
            historyId);

        return await runtime.SubmitAsync(
            operation,
            async () =>
            {
                await ForgetDraftAsync(cancellationToken);
                var history = OfflineProjections.FreeHistory(stamped, workoutName, completedAt, historyId);
                await AddHistoryAsync(history, cancellationToken);
                int? templateId = null;
                if (request.SaveAsTemplate && !string.IsNullOrWhiteSpace(request.TemplateName))
                {
                    var template = OfflineProjections.TemplateFromFreeWorkout(stamped, OfflineIds.Next(), completedAt);
                    await UpdatePlansAsync(x => x.Upsert(template), cancellationToken);
                    templateId = template.Id;
                }

                return new CompleteFreeWorkoutResponse(history, templateId);
            },
            async body =>
            {
                var completed = JsonSerializer.Deserialize<CompleteFreeWorkoutResponse>(body, Json)
                    ?? throw new JsonException("Empty free workout response.");
                await ForgetDraftAsync(cancellationToken);
                await AddHistoryAsync(completed.History, cancellationToken);
                return completed;
            },
            cancellationToken);
    }

    /// <summary>
    /// Свободная тренировка без связи начинается на телефоне: черновик с временным
    /// ID живёт только здесь, а на сервер уходит уже готовая тренировка.
    /// </summary>
    public async Task<ApiResult<FreeWorkoutDraftResponse>> StartFreeWorkoutDraftAsync(
        CancellationToken cancellationToken = default)
    {
        if (!runtime.Reachability.IsOffline)
        {
            using var scope = TransportScope.Begin();
            var result = await runtime.WriteAsync(
                inner.StartFreeWorkoutDraftAsync,
                draft => runtime.Store.SetAsync(
                    OfflineKeys.FreeWorkoutDraft,
                    new OfflineOptional<FreeWorkoutDraftResponse>(draft),
                    cancellationToken),
                cancellationToken);
            if (result.Succeeded || !scope.ServerUnavailable)
                return result;
        }

        var name = AppTexts.Get("Today_FreeWorkout");
        var plan = new TrainingPlanApiResponse(OfflineIds.Next(), name, timeProvider.GetLocalNow().Date, []);
        var localDraft = new FreeWorkoutDraftResponse(plan.Id, 0, name, false);
        await runtime.UpdateCopyAsync<OfflinePlans>(
            OfflineKeys.Plans,
            x => (x ?? OfflinePlans.Empty).Upsert(plan, listed: false),
            cancellationToken);
        await runtime.UpdateCopyAsync<OfflineOptional<FreeWorkoutDraftResponse>>(
            OfflineKeys.FreeWorkoutDraft,
            _ => new(localDraft),
            cancellationToken);
        return ApiResult<FreeWorkoutDraftResponse>.Success(localDraft);
    }

    public async Task<OptionalApiResult<FreeWorkoutDraftResponse>> GetFreeWorkoutDraftAsync(
        CancellationToken cancellationToken = default)
    {
        // Черновик, начатый без связи, сервер не знает: его ответ «черновика нет»
        // не должен стереть идущую тренировку.
        if (await LoadLocalDraftAsync(cancellationToken) is { } localDraft)
            return OptionalApiResult<FreeWorkoutDraftResponse>.Success(localDraft);

        return await runtime.ReadOptionalAsync(
            inner.GetFreeWorkoutDraftAsync,
            async () => await runtime.Store.GetAsync<OfflineOptional<FreeWorkoutDraftResponse>>(
                    OfflineKeys.FreeWorkoutDraft,
                    cancellationToken) is { } stored
                ? (true, stored.Value)
                : (false, null),
            draft => runtime.Store.SetAsync(
                OfflineKeys.FreeWorkoutDraft,
                new OfflineOptional<FreeWorkoutDraftResponse>(draft),
                cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult> DiscardFreeWorkoutDraftAsync(CancellationToken cancellationToken = default)
    {
        if (await LoadLocalDraftAsync(cancellationToken) is not null)
        {
            await ForgetDraftAsync(cancellationToken);
            return ApiResult.Success;
        }

        // Отмена всегда удаётся на телефоне; сервер узнает о ней, когда сможет.
        // Если черновика там уже нет, это тоже нормальный исход.
        var operation = runtime.Outbox.Create(
            OutboxKinds.DiscardFreeWorkoutDraft,
            HttpMethod.Delete,
            "api/v1/workouts/free/draft",
            body: null);
        await runtime.SubmitAsync(operation, () => ForgetDraftAsync(cancellationToken), cancellationToken);
        await ForgetDraftAsync(cancellationToken);
        return ApiResult.Success;
    }

    private async Task<FreeWorkoutDraftResponse?> LoadLocalDraftAsync(CancellationToken cancellationToken)
    {
        var stored = await runtime.Store.GetAsync<OfflineOptional<FreeWorkoutDraftResponse>>(
            OfflineKeys.FreeWorkoutDraft,
            cancellationToken);
        return stored?.Value is { } draft && OfflineIds.IsLocal(draft.TrainingPlanId) ? draft : null;
    }

    private async Task ForgetDraftAsync(CancellationToken cancellationToken)
    {
        var stored = await runtime.Store.GetAsync<OfflineOptional<FreeWorkoutDraftResponse>>(
            OfflineKeys.FreeWorkoutDraft,
            cancellationToken);
        if (stored?.Value is { } draft)
            await UpdatePlansAsync(x => x.Remove(draft.TrainingPlanId), cancellationToken);

        await runtime.UpdateCopyAsync<OfflineOptional<FreeWorkoutDraftResponse>>(
            OfflineKeys.FreeWorkoutDraft,
            _ => new(null),
            cancellationToken);
    }

    public Task<ApiResult<IReadOnlyList<WorkoutHistoryApiResponse>>> GetHistoryAsync(
        CancellationToken cancellationToken = default) =>
        runtime.ReadAsync<IReadOnlyList<WorkoutHistoryApiResponse>>(
            inner.GetHistoryAsync,
            async () => await runtime.Store.GetAsync<List<WorkoutHistoryApiResponse>>(
                OfflineKeys.History,
                cancellationToken),
            history => runtime.Store.SetAsync(OfflineKeys.History, history.ToList(), cancellationToken),
            cancellationToken);

    public async Task<ApiResult> DeleteHistoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var originalId = id;
        id = await runtime.Outbox.ResolveAsync(id, cancellationToken);
        var history = await runtime.Store.GetAsync<List<WorkoutHistoryApiResponse>>(OfflineKeys.History, cancellationToken);
        var operation = runtime.Outbox.Create(
            OutboxKinds.DeleteHistory,
            HttpMethod.Delete,
            $"api/v1/history/{id}",
            body: null,
            history?.FirstOrDefault(x => x.Id == id || x.Id == originalId)?.WorkoutName);
        return await runtime.SubmitAsync(
            operation,
            () => runtime.UpdateCopyAsync<List<WorkoutHistoryApiResponse>>(
                OfflineKeys.History,
                x => (x ?? []).Where(item => item.Id != id && item.Id != originalId).ToList(),
                cancellationToken),
            cancellationToken,
            notFoundIsSuccess: true);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private Task UpdatePlansAsync(Func<OfflinePlans, OfflinePlans> update, CancellationToken cancellationToken) =>
        runtime.UpdateCopyAsync<OfflinePlans>(
            OfflineKeys.Plans,
            current => update(current ?? OfflinePlans.Empty),
            cancellationToken);

    private Task UpdateCalendarAsync(Func<OfflineCalendar, OfflineCalendar> update, CancellationToken cancellationToken) =>
        runtime.Store.UpdateAsync<OfflineCalendar>(
            OfflineKeys.Calendar,
            current => update(current ?? OfflineCalendar.Empty),
            cancellationToken);

    private Task AddHistoryAsync(WorkoutHistoryApiResponse history, CancellationToken cancellationToken) =>
        runtime.Store.UpdateAsync<List<WorkoutHistoryApiResponse>>(
            OfflineKeys.History,
            current => [history, .. (current ?? []).Where(x => x.Id != history.Id)],
            cancellationToken);
}
