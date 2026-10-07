using GymPlanner.Mobile.Api;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Localization;

namespace GymPlanner.Mobile.Tutorial;

/// <summary>
/// Сервер понарошку для обучения, запущенного с «Сегодня».
/// </summary>
/// <remarks>
/// Демонстрация нажимает настоящие кнопки страниц, а они ходят в API: создают
/// черновик свободной тренировки, упражнения, суперсет, пишут историю,
/// назначают шаблон в календаре. Пока идёт обучение, страницы обращаются
/// сюда, и всё это живёт только в памяти этого объекта — ни сервер, ни
/// офлайн-очередь, ни часы ничего не узнают. В календаре виден только
/// демо-шаблон «Грудь и спина» и то, что назначила демонстрация; на
/// «Тренировках» список пуст, пока ролик не создаст свой шаблон.
/// Всё, что демонстрации не нужно, отвечает отказом, а не обращается к сети.
/// </remarks>
/// <param name="withDemoTemplate">
/// Есть ли демо-шаблон «Грудь и спина» — его назначает ролик «Сегодня».
/// </param>
public sealed class TutorialWorkoutBackend(IAppText text, bool withDemoTemplate) : IWorkoutApiClient, IWorkoutLifecycleApiClient
{
    private const int PlanId = 1;
    public const int TemplatePlanId = 2;
    // Шаблон, который создаёт ролик «Тренировок».
    private const int CreatedPlanId = 3;
    private const int TemplateDayId = 1;
    private const int TemplateSupersetGroupId = 1;

    private readonly List<ExerciseApiResponse> _exercises = [];
    private int _nextExerciseId = 1;
    // День, на который демонстрация назначила шаблон.
    private WorkoutDayApiResponse? _scheduledDay;
    // Имя шаблона, созданного роликом «Тренировок»; его упражнения — в _exercises.
    private string? _createdPlanName;

    /// <summary>
    /// Упражнения библиотеки, из которых демонстрация собирает тренировку.
    /// Жим на наклонной скамье — для ролика «Тренировок»: на его фото виден
    /// угол спинки.
    /// </summary>
    public IReadOnlyList<ExerciseDefinitionApiResponse> Definitions { get; } =
    [
        new(1, text["Tutorial_Today_ExerciseBench"]),
        new(2, text["Tutorial_Today_ExerciseRow"]),
        new(3, text["Tutorial_Today_ExerciseDips"]),
        new(4, text["Tutorial_Workouts_ExerciseInclineBench"])
    ];

    public Task<ApiResult<IReadOnlyList<ExerciseDefinitionApiResponse>>> GetExerciseDefinitionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<ExerciseDefinitionApiResponse>>.Success(Definitions));

    public Task<ApiResult<FreeWorkoutDraftResponse>> StartFreeWorkoutDraftAsync(CancellationToken cancellationToken = default)
    {
        _exercises.Clear();
        return Task.FromResult(ApiResult<FreeWorkoutDraftResponse>.Success(
            new FreeWorkoutDraftResponse(PlanId, PlanId, text["Today_FreeWorkout"], AlreadyStarted: false)));
    }

    public Task<OptionalApiResult<FreeWorkoutDraftResponse>> GetFreeWorkoutDraftAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(OptionalApiResult<FreeWorkoutDraftResponse>.Empty());

    public Task<ApiResult> DiscardFreeWorkoutDraftAsync(CancellationToken cancellationToken = default)
    {
        _exercises.Clear();
        return Task.FromResult(ApiResult.Success);
    }

    // Шаблон, назначенный демонстрацией на сегодня, ждёт на «Сегодня».
    public Task<OptionalApiResult<TodayWorkoutApiResponse>> StartTodayWorkoutAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_scheduledDay is { IsCompleted: false } day && day.Date.Date == DateTime.Today
            ? OptionalApiResult<TodayWorkoutApiResponse>.Success(StartTemplateWorkout(day))
            : OptionalApiResult<TodayWorkoutApiResponse>.Empty());

    public Task<ApiResult<ExerciseApiResponse>> CreateExerciseAsync(int planId, SaveExerciseRequest request, CancellationToken cancellationToken = default)
    {
        var exercise = ToResponse(_nextExerciseId++, planId, request, RestTimerDefaults.BetweenSetsSeconds, null);
        _exercises.Add(exercise);
        return Task.FromResult(ApiResult<ExerciseApiResponse>.Success(exercise));
    }

    public Task<ApiResult<ExerciseApiResponse>> UpdateExerciseAsync(int id, SaveExerciseRequest request, CancellationToken cancellationToken = default)
    {
        var index = _exercises.FindIndex(x => x.Id == id);
        if (index < 0)
            return Task.FromResult(ApiResult<ExerciseApiResponse>.Failure(text["Tutorial_Today_Unavailable"]));

        var current = _exercises[index];
        var exercise = ToResponse(
            id,
            current.TrainingPlanId,
            request,
            request.RestBetweenSetsSeconds ?? current.RestBetweenSetsSeconds,
            request.RestAfterExerciseSeconds ?? current.RestAfterExerciseSeconds);
        _exercises[index] = exercise;
        return Task.FromResult(ApiResult<ExerciseApiResponse>.Success(exercise));
    }

    public Task<ApiResult> DeleteExerciseAsync(int id, CancellationToken cancellationToken = default)
    {
        _exercises.RemoveAll(x => x.Id == id);
        return Task.FromResult(ApiResult.Success);
    }

    // Длительности страница уже применила у себя, здесь их хранить незачем.
    public Task<ApiResult<TrainingPlanApiResponse>> UpdateRestTimersAsync(int planId, UpdateRestTimersRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<TrainingPlanApiResponse>.Success(Plan()));

    public Task<ApiResult<TrainingPlanApiResponse>> ReorderSupersetAsync(int planId, int supersetGroupId, ReorderSupersetRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<TrainingPlanApiResponse>.Success(Plan()));

    public Task<ApiResult<TrainingPlanApiResponse>> GetPlanAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<TrainingPlanApiResponse>.Success(
            id == CreatedPlanId && CreatedPlan() is { } created ? created : Plan()));

    public Task<ApiResult<CompleteFreeWorkoutResponse>> CompleteFreeWorkoutAsync(CompleteFreeWorkoutRequest request, CancellationToken cancellationToken = default)
    {
        var history = new WorkoutHistoryApiResponse(
            PlanId,
            text["Today_FreeWorkout"],
            request.CompletedAt ?? DateTime.Now,
            string.Empty,
            SnapshotAvailable: false,
            []);
        _exercises.Clear();
        return Task.FromResult(ApiResult<CompleteFreeWorkoutResponse>.Success(new CompleteFreeWorkoutResponse(history, null)));
    }

    public Task<ApiResult<IReadOnlyList<TrainingPlanApiResponse>>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = new List<TrainingPlanApiResponse>();
        if (withDemoTemplate)
            plans.Add(TemplatePlan(TemplateExercises()));
        if (CreatedPlan() is { } created)
            plans.Add(created);
        return Task.FromResult(ApiResult<IReadOnlyList<TrainingPlanApiResponse>>.Success(plans));
    }

    public Task<ApiResult<TrainingPlanApiResponse>> CreatePlanAsync(CreateTrainingPlanRequest request, CancellationToken cancellationToken = default)
    {
        _createdPlanName = request.WorkoutName;
        _exercises.Clear();
        return Task.FromResult(ApiResult<TrainingPlanApiResponse>.Success(CreatedPlan()!));
    }

    public Task<ApiResult<TrainingPlanApiResponse>> RenamePlanAsync(int id, RenameTrainingPlanRequest request, CancellationToken cancellationToken = default) =>
        Unavailable<TrainingPlanApiResponse>();

    public Task<ApiResult> DeletePlanAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Failure(text["Tutorial_Today_Unavailable"]));

    public Task<ApiResult<IReadOnlyList<WorkoutDayApiResponse>>> GetCalendarAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<WorkoutDayApiResponse>>.Success(
            _scheduledDay is { } day && day.Date.Date >= from.Date && day.Date.Date <= to.Date ? [day] : []));

    public Task<ApiResult<WorkoutDayApiResponse>> ScheduleWorkoutAsync(ScheduleWorkoutRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TrainingPlanId != TemplatePlanId)
            return Unavailable<WorkoutDayApiResponse>();

        _scheduledDay = new WorkoutDayApiResponse(TemplateDayId, request.Date.Date, TemplatePlanId, IsCompleted: false);
        return Task.FromResult(ApiResult<WorkoutDayApiResponse>.Success(_scheduledDay));
    }

    public Task<ApiResult> DeleteCalendarDayAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Failure(text["Tutorial_Today_Unavailable"]));

    public Task<ApiResult<WorkoutDayApiResponse>> MoveCalendarDayAsync(int id, MoveWorkoutRequest request, CancellationToken cancellationToken = default) =>
        Unavailable<WorkoutDayApiResponse>();

    public Task<ApiResult<WorkoutHistoryApiResponse>> CompleteTodayWorkoutAsync(CancellationToken cancellationToken = default) =>
        Unavailable<WorkoutHistoryApiResponse>();

    public Task<ApiResult<WorkoutHistoryApiResponse>> CompleteScheduledWorkoutAsync(int workoutDayId, CompleteScheduledWorkoutRequest request, CancellationToken cancellationToken = default)
    {
        var history = new WorkoutHistoryApiResponse(
            TemplateDayId,
            text["Tutorial_Today_TemplateName"],
            request.CompletedAt,
            string.Empty,
            SnapshotAvailable: false,
            []);
        _exercises.Clear();
        if (_scheduledDay is { } day)
            _scheduledDay = day with { IsCompleted = true };
        return Task.FromResult(ApiResult<WorkoutHistoryApiResponse>.Success(history));
    }

    /// <summary>
    /// Тренировка по шаблону, назначенная демонстрацией на сегодня: жим
    /// отдельно, тяга и отжимания — суперсетом. Вес, повторы и отдых берутся
    /// «из шаблона», поэтому на странице всё уже заполнено.
    /// </summary>
    private TodayWorkoutApiResponse StartTemplateWorkout(WorkoutDayApiResponse day)
    {
        _exercises.Clear();
        _exercises.AddRange(TemplateExercises());
        return new TodayWorkoutApiResponse(day, TemplatePlan(_exercises.ToList()));
    }

    private TrainingPlanApiResponse TemplatePlan(IReadOnlyList<ExerciseApiResponse> exercises) =>
        new(TemplatePlanId, text["Tutorial_Today_TemplateName"], DateTime.Today, exercises);

    private List<ExerciseApiResponse> TemplateExercises() =>
    [
        TemplateExercise(101, Definitions[0], [(60, 10), (70, 8), (75, 6)], supersetGroupId: null),
        TemplateExercise(102, Definitions[1], [(24, 12), (26, 10)], supersetGroupId: TemplateSupersetGroupId),
        TemplateExercise(103, Definitions[2], [(0, 12), (0, 10)], supersetGroupId: TemplateSupersetGroupId)
    ];

    private static ExerciseApiResponse TemplateExercise(
        int id,
        ExerciseDefinitionApiResponse definition,
        IReadOnlyList<(double Weight, int Repetitions)> sets,
        int? supersetGroupId) =>
        new(
            id,
            definition.Name,
            sets.Count,
            "NotCompleted",
            TemplatePlanId,
            definition.Id,
            HasPhoto: false,
            sets.Select((set, index) => new ExerciseSetApiResponse(index + 1, set.Repetitions, set.Weight, Completed: false)).ToList(),
            supersetGroupId);

    public Task<ApiResult<IReadOnlyList<WorkoutHistoryApiResponse>>> GetHistoryAsync(CancellationToken cancellationToken = default) =>
        Unavailable<IReadOnlyList<WorkoutHistoryApiResponse>>();

    public Task<ApiResult> DeleteHistoryAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Failure(text["Tutorial_Today_Unavailable"]));

    private Task<ApiResult<T>> Unavailable<T>() =>
        Task.FromResult(ApiResult<T>.Failure(text["Tutorial_Today_Unavailable"]));

    private TrainingPlanApiResponse Plan() =>
        new(PlanId, text["Today_FreeWorkout"], DateTime.Today, _exercises.ToList());

    private TrainingPlanApiResponse? CreatedPlan() =>
        _createdPlanName is { } name
            ? new(CreatedPlanId, name, DateTime.Today, _exercises.ToList())
            : null;

    private static ExerciseApiResponse ToResponse(int id, int planId, SaveExerciseRequest request, int restBetweenSets, int? restAfterExercise) =>
        new(
            id,
            request.Name,
            request.Sets?.Count ?? request.SetsCount,
            request.Status,
            planId,
            request.ExerciseDefinitionId,
            HasPhoto: false,
            (request.Sets ?? [])
                .Select(set => new ExerciseSetApiResponse(
                    set.SetNumber,
                    set.Repetitions,
                    set.Weight,
                    set.Completed,
                    set.IsWarmup,
                    set.RestAfterSeconds))
                .ToList(),
            request.SupersetGroupId,
            restBetweenSets,
            restAfterExercise);
}
