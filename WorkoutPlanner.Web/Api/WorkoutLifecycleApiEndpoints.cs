using System.Text.Json;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Api;

public static class WorkoutLifecycleApiEndpoints
{
    public static RouteGroupBuilder MapWorkoutLifecycleApiEndpoints(
        this RouteGroupBuilder api)
    {
        var calendar = api.MapGroup("/calendar")
            .WithTags("Calendar")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);
        calendar.MapGet("", GetCalendarAsync)
            .Produces<List<WorkoutDayApiResponse>>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        calendar.MapGet("/{id:int}", GetCalendarDayAsync)
            .Produces<WorkoutDayApiResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        calendar.MapPost("", ScheduleWorkoutAsync)
            .Produces<WorkoutDayApiResponse>(StatusCodes.Status201Created)
            .Produces<WorkoutDayApiResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        calendar.MapDelete("/{id:int}", DeleteCalendarDayAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        calendar.MapPut("/{id:int}/date", MoveCalendarDayAsync)
            .Produces<WorkoutDayApiResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        var workout = api.MapGroup("/workouts/today")
            .WithTags("Workout lifecycle")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);
        workout.MapGet("", GetTodayWorkoutAsync)
            .Produces<TodayWorkoutApiResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        workout.MapPost("/start", GetTodayWorkoutAsync)
            .Produces<TodayWorkoutApiResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        workout.MapPost("/complete", CompleteTodayWorkoutAsync)
            .Produces<WorkoutHistoryApiResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        var freeWorkout = api.MapGroup("/workouts/free")
            .WithTags("Workout lifecycle")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);
        freeWorkout.MapPost("/complete", CompleteFreeWorkoutAsync)
            .Produces<CompleteFreeWorkoutResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        var history = api.MapGroup("/history")
            .WithTags("Workout history")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);
        history.MapGet("", GetHistoryAsync)
            .Produces<List<WorkoutHistoryApiResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        history.MapGet("/{id:int}", GetHistoryItemAsync)
            .Produces<WorkoutHistoryApiResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        history.MapDelete("/{id:int}", DeleteHistoryItemAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return api;
    }

    private static async Task<IResult> GetCalendarAsync(
        DateTime? from,
        DateTime? to,
        IWorkoutDayService workoutDays,
        CancellationToken cancellationToken)
    {
        if (from.HasValue != to.HasValue)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["range"] = ["Both from and to must be supplied together."]
            });
        }

        List<WorkoutDay> days;
        if (from is null)
        {
            days = await workoutDays.GetDaysAsync(cancellationToken);
        }
        else
        {
            var start = from.Value.Date;
            var end = to!.Value.Date;
            if (start > end || (end - start).TotalDays > 366)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["range"] =
                            ["The calendar range must be ordered and no longer than 366 days."]
                    });
            }

            days = await workoutDays.GetDaysAsync(
                from.Value,
                to.Value,
                cancellationToken);
        }

        return Results.Ok(days.Select(ToResponse).ToList());
    }

    private static async Task<IResult> GetCalendarDayAsync(
        int id,
        IWorkoutDayService workoutDays,
        CancellationToken cancellationToken)
    {
        var day = await workoutDays.GetByIdAsync(id, cancellationToken);
        return day is null ? Results.NotFound() : Results.Ok(ToResponse(day));
    }

    private static async Task<IResult> ScheduleWorkoutAsync(
        ScheduleWorkoutRequest request,
        IWorkoutDayService workoutDays,
        ITrainingPlanService trainingPlans,
        CancellationToken cancellationToken)
    {
        if (request.Date == default)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Date)] = ["A valid workout date is required."]
            });
        }

        if (await trainingPlans.GetByIdAsync(
                request.TrainingPlanId,
                cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var existing = (await workoutDays.GetDaysAsync(
                request.Date,
                request.Date,
                cancellationToken))
            .FirstOrDefault();
        var saved = await workoutDays.SaveDayAsync(
            request.Date.Date,
            request.TrainingPlanId,
            cancellationToken);
        var response = ToResponse(saved);
        return existing is null
            ? Results.Created($"/api/v1/calendar/{saved.Id}", response)
            : Results.Ok(response);
    }

    private static async Task<IResult> DeleteCalendarDayAsync(
        int id,
        IWorkoutDayService workoutDays,
        CancellationToken cancellationToken)
    {
        return await workoutDays.DeleteDayAsync(id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static async Task<IResult> MoveCalendarDayAsync(
        int id,
        MoveWorkoutRequest request,
        IWorkoutDayService workoutDays,
        CancellationToken cancellationToken)
    {
        if (request.Date == default || request.Date.Date < DateTime.Today)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Date)] = ["A current or future workout date is required."]
            });
        }

        var moved = await workoutDays.MoveDayAsync(id, request.Date, cancellationToken);
        return moved is null
            ? Results.Problem(
                title: "The workout cannot be moved because the target date is occupied or the workout is unavailable.",
                statusCode: StatusCodes.Status409Conflict)
            : Results.Ok(ToResponse(moved));
    }

    private static async Task<IResult> GetTodayWorkoutAsync(
        IWorkoutDayService workoutDays,
        ITodayWorkoutService todayWorkout,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var day = (await workoutDays.GetDaysAsync(
                today,
                today,
                cancellationToken))
            .FirstOrDefault(x => !x.IsCompleted);
        var plan = await todayWorkout.GetTodayWorkoutAsync(cancellationToken);
        if (day is null || plan is null)
            return Results.NotFound();

        return Results.Ok(new TodayWorkoutApiResponse(
            ToResponse(day),
            WorkoutApiEndpoints.ToApiResponse(plan)));
    }

    private static async Task<IResult> CompleteTodayWorkoutAsync(
        IWorkoutCompletionService completion,
        CancellationToken cancellationToken)
    {
        var result = await completion.CompleteTodayAsync(cancellationToken);
        if (!result.Succeeded || result.History is null)
        {
            var title = result.Failure switch
            {
                WorkoutCompletionFailure.NoExercises =>
                    "The scheduled workout has no exercises.",
                WorkoutCompletionFailure.ExerciseStatusMissing =>
                    "Every exercise must have a status before completion.",
                _ => "There is no incomplete workout scheduled for today."
            };
            return Results.Problem(
                title: title,
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created(
            $"/api/v1/history/{result.History.Id}",
            ToResponse(result.History));
    }

    private static async Task<IResult> CompleteFreeWorkoutAsync(
        CompleteFreeWorkoutRequest request,
        IWorkoutCompletionService completion,
        CancellationToken cancellationToken)
    {
        if (request.Exercises is null || request.Exercises.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Exercises)] =
                    ["Добавьте хотя бы одно упражнение."]
            });
        }

        var exercises = new List<Exercise>(request.Exercises.Count);
        foreach (var source in request.Exercises)
        {
            if (!Enum.TryParse<ExerciseStatus>(
                    source.Status,
                    ignoreCase: true,
                    out var status) ||
                !Enum.IsDefined(status) ||
                source.Sets is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Exercises)] =
                        ["Проверьте оценку и подходы каждого упражнения."]
                });
            }

            exercises.Add(new Exercise
            {
                Name = source.Name,
                SetsCount = source.SetsCount,
                Status = status,
                ExerciseDefinitionId = source.ExerciseDefinitionId,
                SupersetGroupId = source.SupersetGroupId,
                Sets = source.Sets
                    .Select(set => new ExerciseTemplateSet
                    {
                        SetNumber = set.SetNumber,
                        Repetitions = set.Repetitions,
                        Weight = set.Weight,
                        Completed = set.Completed,
                        IsWarmup = set.IsWarmup
                    })
                    .ToList()
            });
        }

        var result = await completion.CompleteFreeAsync(
            new FreeWorkoutCompletion
            {
                SaveAsTemplate = request.SaveAsTemplate,
                TemplateName = request.TemplateName,
                Exercises = exercises
            },
            cancellationToken);
        if (!result.Succeeded || result.History is null)
        {
            if (result.Failure == FreeWorkoutCompletionFailure.TemplateNameConflict)
            {
                return Results.Problem(
                    title: result.Error,
                    statusCode: StatusCodes.Status409Conflict);
            }

            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["workout"] = [result.Error ?? "Не удалось сохранить тренировку."]
            });
        }

        return Results.Created(
            $"/api/v1/history/{result.History.Id}",
            new CompleteFreeWorkoutResponse(
                ToResponse(result.History),
                result.TrainingPlanId));
    }

    private static async Task<IResult> GetHistoryAsync(
        IHistoryService history,
        CancellationToken cancellationToken)
    {
        var items = await history.GetHistoryAsync(cancellationToken);
        return Results.Ok(items.Select(ToResponse).ToList());
    }

    private static async Task<IResult> GetHistoryItemAsync(
        int id,
        IHistoryService history,
        CancellationToken cancellationToken)
    {
        var item = await history.GetByIdAsync(id, cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(ToResponse(item));
    }

    private static async Task<IResult> DeleteHistoryItemAsync(
        int id,
        IHistoryService history,
        CancellationToken cancellationToken)
    {
        return await history.DeleteHistoryAsync(id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static WorkoutDayApiResponse ToResponse(WorkoutDay day) =>
        new(day.Id, day.Date, day.TrainingPlanId, day.IsCompleted);

    private static WorkoutHistoryApiResponse ToResponse(WorkoutHistory history)
    {
        try
        {
            var details = JsonSerializer.Deserialize<WorkoutHistoryDetails>(
                history.Details);
            if (details is not null)
            {
                return new(
                    history.Id,
                    history.WorkoutName,
                    history.Date,
                    history.Summary,
                    true,
                    (details.Exercises ?? [])
                        .OfType<WorkoutHistoryExercise>()
                        .Select(x =>
                            new WorkoutHistoryExerciseApiResponse(
                                x.Name,
                                x.Status.ToString(),
                                (x.Sets ?? [])
                                    .OrderBy(set => set.SetNumber)
                                    .Select(set => new ExerciseSetApiResponse(
                                        set.SetNumber,
                                        set.Repetitions,
                                        set.Weight,
                                        set.Completed,
                                        set.IsWarmup))
                                    .ToList()))
                        .ToList());
            }
        }
        catch (JsonException)
        {
            // Legacy snapshots remain stored unchanged and are reported as unavailable.
        }

        return new(
            history.Id,
            history.WorkoutName,
            history.Date,
            history.Summary,
            false,
            []);
    }
}
