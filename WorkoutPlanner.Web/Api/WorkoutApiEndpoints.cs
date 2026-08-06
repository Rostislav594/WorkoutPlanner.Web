using WorkoutPlanner.Web.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using AppContracts = WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Api;

public static class WorkoutApiEndpoints
{
    public static RouteGroupBuilder MapWorkoutApiEndpoints(
        this RouteGroupBuilder api)
    {
        var plans = api.MapGroup("/training-plans")
            .WithTags("Training plans")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);

        plans.MapGet("", GetTrainingPlansAsync)
            .Produces<List<TrainingPlanApiResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        plans.MapGet("/{id:int}", GetTrainingPlanAsync)
            .Produces<TrainingPlanApiResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        plans.MapPost("", CreateTrainingPlanAsync)
            .Produces<TrainingPlanApiResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        plans.MapPut("/{id:int}", RenameTrainingPlanAsync)
            .Produces<TrainingPlanApiResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        plans.MapDelete("/{id:int}", DeleteTrainingPlanAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        plans.MapGet("/{planId:int}/exercises", GetExercisesAsync)
            .Produces<List<ExerciseApiResponse>>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        plans.MapPost("/{planId:int}/exercises", CreateExerciseAsync)
            .Produces<ExerciseApiResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        var exercises = api.MapGroup("/exercises")
            .WithTags("Exercises")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);
        exercises.MapGet("/{id:int}", GetExerciseAsync)
            .Produces<ExerciseApiResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        exercises.MapPut("/{id:int}", UpdateExerciseAsync)
            .Produces<ExerciseApiResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        exercises.MapDelete("/{id:int}", DeleteExerciseAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        api.MapGet("/exercise-definitions", GetExerciseDefinitionsAsync)
            .WithTags("Exercises")
            .RequireAuthorization(MobileApiAuthorization.PolicyName)
            .Produces<List<ExerciseDefinitionApiResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return api;
    }

    private static async Task<IResult> GetTrainingPlansAsync(
        ITrainingPlanService trainingPlans,
        CancellationToken cancellationToken)
    {
        var plans = await trainingPlans.GetTrainingPlansAsync(cancellationToken);
        return Results.Ok(plans.Select(ToResponse).ToList());
    }

    private static async Task<IResult> GetTrainingPlanAsync(
        int id,
        ITrainingPlanService trainingPlans,
        CancellationToken cancellationToken)
    {
        var plan = await trainingPlans.GetByIdAsync(id, cancellationToken);
        return plan is null ? Results.NotFound() : Results.Ok(ToResponse(plan));
    }

    private static async Task<IResult> CreateTrainingPlanAsync(
        CreateTrainingPlanRequest request,
        ITrainingPlanService trainingPlans,
        CancellationToken cancellationToken)
    {
        var errors = ValidateWorkoutName(request.WorkoutName);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var normalizedName = request.WorkoutName.Trim();
        var result = await trainingPlans.CreateAsync(
            normalizedName,
            cancellationToken);
        if (!result.Succeeded)
        {
            return Results.Problem(
                title: "A training plan with this name already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var created = (await trainingPlans.GetTrainingPlansAsync(cancellationToken))
            .Where(x => x.WorkoutName == normalizedName)
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();
        if (created is null)
        {
            return Results.Problem(
                title: "The training plan could not be loaded after creation.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Created(
            $"/api/v1/training-plans/{created.Id}",
            ToResponse(created));
    }

    private static async Task<IResult> RenameTrainingPlanAsync(
        int id,
        RenameTrainingPlanRequest request,
        ITrainingPlanService trainingPlans,
        CancellationToken cancellationToken)
    {
        if (await trainingPlans.GetByIdAsync(id, cancellationToken) is null)
            return Results.NotFound();

        var errors = ValidateWorkoutName(request.WorkoutName);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await trainingPlans.RenameAsync(
            id,
            request.WorkoutName.Trim(),
            cancellationToken);
        if (!result.Succeeded)
        {
            return Results.Problem(
                title: "A training plan with this name already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var renamed = await trainingPlans.GetByIdAsync(id, cancellationToken);
        return renamed is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(renamed));
    }

    private static async Task<IResult> DeleteTrainingPlanAsync(
        int id,
        ITrainingPlanService trainingPlans,
        CancellationToken cancellationToken)
    {
        if (await trainingPlans.GetByIdAsync(id, cancellationToken) is null)
            return Results.NotFound();

        await trainingPlans.DeleteAsync(id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetExercisesAsync(
        int planId,
        ITrainingPlanService trainingPlans,
        CancellationToken cancellationToken)
    {
        var plan = await trainingPlans.GetByIdAsync(planId, cancellationToken);
        return plan is null
            ? Results.NotFound()
            : Results.Ok(plan.Exercises.Select(ToResponse).ToList());
    }

    private static async Task<IResult> CreateExerciseAsync(
        int planId,
        SaveExerciseRequest request,
        ITrainingPlanService trainingPlans,
        IExerciseService exercises,
        IExerciseDefinitionService exerciseDefinitions,
        CancellationToken cancellationToken)
    {
        var plan = await trainingPlans.GetByIdAsync(planId, cancellationToken);
        if (plan is null)
            return Results.NotFound();

        var validation = await ValidateExerciseAsync(
            request,
            exerciseDefinitions,
            cancellationToken);
        if (validation.Errors.Count > 0)
            return Results.ValidationProblem(validation.Errors);

        var exercise = ToContract(request, validation.Status, plan, id: 0);
        await exercises.AddExerciseAsync(exercise, cancellationToken);
        return Results.Created(
            $"/api/v1/exercises/{exercise.Id}",
            ToResponse(exercise));
    }

    private static async Task<IResult> GetExerciseAsync(
        int id,
        IExerciseService exercises,
        CancellationToken cancellationToken)
    {
        var exercise = await exercises.GetByIdAsync(id, cancellationToken);
        return exercise is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(exercise));
    }

    private static async Task<IResult> UpdateExerciseAsync(
        int id,
        SaveExerciseRequest request,
        IExerciseService exercises,
        IExerciseDefinitionService exerciseDefinitions,
        CancellationToken cancellationToken)
    {
        var existing = await exercises.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return Results.NotFound();

        var validation = await ValidateExerciseAsync(
            request,
            exerciseDefinitions,
            cancellationToken);
        if (validation.Errors.Count > 0)
            return Results.ValidationProblem(validation.Errors);

        var updated = ToContract(
            request,
            validation.Status,
            new AppContracts.TrainingPlan
            {
                Id = existing.TrainingPlanId,
                WorkoutName = existing.WorkoutName
            },
            id);
        updated.PhotoPath = existing.PhotoPath;
        await exercises.UpdateExerciseAsync(updated, cancellationToken);
        return Results.Ok(ToResponse(updated));
    }

    private static async Task<IResult> DeleteExerciseAsync(
        int id,
        IExerciseService exercises,
        CancellationToken cancellationToken)
    {
        if (await exercises.GetByIdAsync(id, cancellationToken) is null)
            return Results.NotFound();

        await exercises.DeleteExerciseAsync(id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetExerciseDefinitionsAsync(
        IExerciseDefinitionService exerciseDefinitions,
        CancellationToken cancellationToken)
    {
        var definitions = await exerciseDefinitions.GetAllAsync(cancellationToken);
        return Results.Ok(definitions
            .Select(x => new ExerciseDefinitionApiResponse(x.Id, x.Name))
            .ToList());
    }

    private static TrainingPlanApiResponse ToResponse(
        AppContracts.TrainingPlan plan) =>
        new(
            plan.Id,
            plan.WorkoutName,
            plan.Date,
            plan.Exercises.Select(ToResponse).ToList());

    private static ExerciseApiResponse ToResponse(
        AppContracts.Exercise exercise) =>
        new(
            exercise.Id,
            exercise.Name,
            exercise.SetsCount,
            exercise.Status.ToString(),
            exercise.TrainingPlanId,
            exercise.ExerciseDefinitionId,
            !string.IsNullOrWhiteSpace(exercise.PhotoPath),
            exercise.Sets
                .OrderBy(x => x.SetNumber)
                .Select(x => new ExerciseSetApiResponse(
                    x.SetNumber,
                    x.Repetitions,
                    x.Weight,
                    x.Completed))
                .ToList());

    private static AppContracts.Exercise ToContract(
        SaveExerciseRequest request,
        AppContracts.ExerciseStatus status,
        AppContracts.TrainingPlan plan,
        int id) =>
        new()
        {
            Id = id,
            Name = request.Name.Trim(),
            WorkoutName = plan.WorkoutName,
            SetsCount = request.SetsCount,
            Status = status,
            TrainingPlanId = plan.Id,
            ExerciseDefinitionId = request.ExerciseDefinitionId,
            Sets = request.Sets!
                .OrderBy(x => x.SetNumber)
                .Select(x => new AppContracts.ExerciseTemplateSet
                {
                    SetNumber = x.SetNumber,
                    Repetitions = x.Repetitions,
                    Weight = x.Weight,
                    Completed = x.Completed
                })
                .ToList()
        };

    private static Dictionary<string, string[]> ValidateWorkoutName(
        string? workoutName)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(workoutName) || workoutName.Trim().Length > 120)
        {
            errors[nameof(workoutName)] =
                ["Workout name is required and must not exceed 120 characters."];
        }

        return errors;
    }

    private static async Task<ExerciseValidationResult> ValidateExerciseAsync(
        SaveExerciseRequest request,
        IExerciseDefinitionService exerciseDefinitions,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
        {
            errors[nameof(request.Name)] =
                ["Exercise name is required and must not exceed 160 characters."];
        }

        if (request.SetsCount is < 1 or > 20)
            errors[nameof(request.SetsCount)] = ["Sets count must be between 1 and 20."];

        var statusIsValid = Enum.TryParse<AppContracts.ExerciseStatus>(
            request.Status,
            ignoreCase: true,
            out var status);
        if (!statusIsValid || !Enum.IsDefined(status))
            errors[nameof(request.Status)] = ["Exercise status is invalid."];

        if (request.Sets is null || request.Sets.Count != request.SetsCount)
        {
            errors[nameof(request.Sets)] =
                ["Exactly one individual weight entry is required for every set."];
        }
        else
        {
            var setNumbers = request.Sets
                .Select(x => x.SetNumber)
                .OrderBy(x => x)
                .ToArray();
            if (!setNumbers.SequenceEqual(Enumerable.Range(1, request.SetsCount)))
            {
                errors[nameof(request.Sets)] =
                    ["Set numbers must be unique and sequential starting at 1."];
            }
            else if (request.Sets.Any(x =>
                         x.Repetitions is < 1 or > 1000 ||
                         !double.IsFinite(x.Weight) ||
                         x.Weight is < 0 or > 2000))
            {
                errors[nameof(request.Sets)] =
                    ["Every set must have 1-1000 repetitions and a weight from 0 to 2000."];
            }
        }

        if (request.ExerciseDefinitionId is { } definitionId &&
            !await exerciseDefinitions.ExistsAsync(definitionId, cancellationToken))
        {
            errors[nameof(request.ExerciseDefinitionId)] =
                ["The selected exercise definition does not exist."];
        }

        return new ExerciseValidationResult(errors, status);
    }

    private sealed record ExerciseValidationResult(
        Dictionary<string, string[]> Errors,
        AppContracts.ExerciseStatus Status);
}
