using GymPlanner.Mobile.Api;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Localization;

namespace GymPlanner.Mobile.Tutorial;

/// <summary>
/// Сервер понарошку для обучения на странице «Сегодня».
/// </summary>
/// <remarks>
/// Демонстрация нажимает настоящие кнопки страницы, а они ходят в API: создают
/// черновик свободной тренировки, упражнения, суперсет, пишут историю. Пока идёт
/// обучение, страница обращается сюда, и всё это живёт только в памяти этого
/// объекта — ни сервер, ни офлайн-очередь, ни часы ничего не узнают.
/// Всё, что демонстрации не нужно, отвечает отказом, а не обращается к сети.
/// </remarks>
public sealed class TutorialWorkoutBackend(IAppText text) : IWorkoutApiClient, IWorkoutLifecycleApiClient
{
    private const int PlanId = 1;

    private readonly List<ExerciseApiResponse> _exercises = [];
    private int _nextExerciseId = 1;

    /// <summary>Упражнения библиотеки, из которых демонстрация собирает тренировку.</summary>
    public IReadOnlyList<ExerciseDefinitionApiResponse> Definitions { get; } =
    [
        new(1, text["Tutorial_Today_ExerciseBench"]),
        new(2, text["Tutorial_Today_ExerciseRow"]),
        new(3, text["Tutorial_Today_ExerciseDips"])
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

    public Task<OptionalApiResult<TodayWorkoutApiResponse>> StartTodayWorkoutAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(OptionalApiResult<TodayWorkoutApiResponse>.Empty());

    public Task<ApiResult<ExerciseApiResponse>> CreateExerciseAsync(int planId, SaveExerciseRequest request, CancellationToken cancellationToken = default)
    {
        var exercise = ToResponse(_nextExerciseId++, request, RestTimerDefaults.BetweenSetsSeconds, null);
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
        Task.FromResult(ApiResult<TrainingPlanApiResponse>.Success(Plan()));

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

    public Task<ApiResult<IReadOnlyList<TrainingPlanApiResponse>>> GetPlansAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<TrainingPlanApiResponse>>.Failure(text["Tutorial_Today_Unavailable"]));

    public Task<ApiResult<TrainingPlanApiResponse>> CreatePlanAsync(CreateTrainingPlanRequest request, CancellationToken cancellationToken = default) =>
        Unavailable<TrainingPlanApiResponse>();

    public Task<ApiResult<TrainingPlanApiResponse>> RenamePlanAsync(int id, RenameTrainingPlanRequest request, CancellationToken cancellationToken = default) =>
        Unavailable<TrainingPlanApiResponse>();

    public Task<ApiResult> DeletePlanAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Failure(text["Tutorial_Today_Unavailable"]));

    public Task<ApiResult<IReadOnlyList<WorkoutDayApiResponse>>> GetCalendarAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default) =>
        Unavailable<IReadOnlyList<WorkoutDayApiResponse>>();

    public Task<ApiResult<WorkoutDayApiResponse>> ScheduleWorkoutAsync(ScheduleWorkoutRequest request, CancellationToken cancellationToken = default) =>
        Unavailable<WorkoutDayApiResponse>();

    public Task<ApiResult> DeleteCalendarDayAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Failure(text["Tutorial_Today_Unavailable"]));

    public Task<ApiResult<WorkoutDayApiResponse>> MoveCalendarDayAsync(int id, MoveWorkoutRequest request, CancellationToken cancellationToken = default) =>
        Unavailable<WorkoutDayApiResponse>();

    public Task<ApiResult<WorkoutHistoryApiResponse>> CompleteTodayWorkoutAsync(CancellationToken cancellationToken = default) =>
        Unavailable<WorkoutHistoryApiResponse>();

    public Task<ApiResult<WorkoutHistoryApiResponse>> CompleteScheduledWorkoutAsync(int workoutDayId, CompleteScheduledWorkoutRequest request, CancellationToken cancellationToken = default) =>
        Unavailable<WorkoutHistoryApiResponse>();

    public Task<ApiResult<IReadOnlyList<WorkoutHistoryApiResponse>>> GetHistoryAsync(CancellationToken cancellationToken = default) =>
        Unavailable<IReadOnlyList<WorkoutHistoryApiResponse>>();

    public Task<ApiResult> DeleteHistoryAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Failure(text["Tutorial_Today_Unavailable"]));

    private Task<ApiResult<T>> Unavailable<T>() =>
        Task.FromResult(ApiResult<T>.Failure(text["Tutorial_Today_Unavailable"]));

    private TrainingPlanApiResponse Plan() =>
        new(PlanId, text["Today_FreeWorkout"], DateTime.Today, _exercises.ToList());

    private static ExerciseApiResponse ToResponse(int id, SaveExerciseRequest request, int restBetweenSets, int? restAfterExercise) =>
        new(
            id,
            request.Name,
            request.Sets?.Count ?? request.SetsCount,
            request.Status,
            PlanId,
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
