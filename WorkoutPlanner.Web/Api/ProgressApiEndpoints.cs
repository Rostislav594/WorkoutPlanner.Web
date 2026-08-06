using WorkoutPlanner.Web.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Api;

public static class ProgressApiEndpoints
{
    public static RouteGroupBuilder MapProgressApiEndpoints(
        this RouteGroupBuilder api)
    {
        var progress = api.MapGroup("/progress")
            .WithTags("Progress")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);

        progress.MapGet("/workouts/{trainingPlanId:int}", GetWorkoutProgressAsync)
            .Produces<WorkoutProgressApiResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        progress.MapGet(
                "/workouts/{trainingPlanId:int}/exercises",
                GetWorkoutExercisesAsync)
            .Produces<List<string>>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        progress.MapGet(
                "/workouts/{trainingPlanId:int}/exercises/chart",
                GetExerciseProgressAsync)
            .Produces<ExerciseProgressApiResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        progress.MapDelete("", ClearAllProgressAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        progress.MapDelete(
                "/workouts/{trainingPlanId:int}",
                ClearWorkoutProgressAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        progress.MapDelete(
                "/workouts/{trainingPlanId:int}/exercises",
                ClearExerciseProgressAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return api;
    }

    private static async Task<IResult> GetWorkoutProgressAsync(
        int trainingPlanId,
        ITrainingPlanService trainingPlans,
        IProgressService progress,
        CancellationToken cancellationToken)
    {
        var plan = await trainingPlans.GetByIdAsync(
            trainingPlanId,
            cancellationToken);
        if (plan is null)
            return Results.NotFound();

        var snapshots = await progress.GetWorkoutProgressAsync(
            plan.WorkoutName,
            cancellationToken);
        var chart = await progress.GetWorkoutChartAsync(
            plan.WorkoutName,
            cancellationToken);
        return Results.Ok(new WorkoutProgressApiResponse(
            plan.Id,
            plan.WorkoutName,
            snapshots.Select(ToResponse).ToList(),
            chart.Select(ToResponse).ToList()));
    }

    private static async Task<IResult> GetWorkoutExercisesAsync(
        int trainingPlanId,
        ITrainingPlanService trainingPlans,
        IProgressService progress,
        CancellationToken cancellationToken)
    {
        var plan = await trainingPlans.GetByIdAsync(
            trainingPlanId,
            cancellationToken);
        if (plan is null)
            return Results.NotFound();

        return Results.Ok(await progress.GetWorkoutExercisesAsync(
            plan.WorkoutName,
            cancellationToken));
    }

    private static async Task<IResult> GetExerciseProgressAsync(
        int trainingPlanId,
        string? exerciseName,
        ITrainingPlanService trainingPlans,
        IProgressService progress,
        CancellationToken cancellationToken)
    {
        var errors = ValidateExerciseName(exerciseName);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var plan = await trainingPlans.GetByIdAsync(
            trainingPlanId,
            cancellationToken);
        if (plan is null)
            return Results.NotFound();

        var normalizedName = exerciseName!.Trim();
        var knownExercises = await progress.GetWorkoutExercisesAsync(
            plan.WorkoutName,
            cancellationToken);
        if (!knownExercises.Contains(normalizedName, StringComparer.Ordinal))
            return Results.NotFound();

        var chart = await progress.GetExerciseChartAsync(
            plan.WorkoutName,
            normalizedName,
            cancellationToken);
        return Results.Ok(new ExerciseProgressApiResponse(
            plan.Id,
            plan.WorkoutName,
            normalizedName,
            chart.Select(ToResponse).ToList()));
    }

    private static async Task<IResult> ClearAllProgressAsync(
        IProgressService progress,
        CancellationToken cancellationToken)
    {
        await progress.ClearAllProgressAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearWorkoutProgressAsync(
        int trainingPlanId,
        ITrainingPlanService trainingPlans,
        IProgressService progress,
        CancellationToken cancellationToken)
    {
        var plan = await trainingPlans.GetByIdAsync(
            trainingPlanId,
            cancellationToken);
        if (plan is null)
            return Results.NotFound();

        await progress.ClearWorkoutProgressAsync(
            plan.WorkoutName,
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearExerciseProgressAsync(
        int trainingPlanId,
        string? exerciseName,
        ITrainingPlanService trainingPlans,
        IProgressService progress,
        CancellationToken cancellationToken)
    {
        var errors = ValidateExerciseName(exerciseName);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var plan = await trainingPlans.GetByIdAsync(
            trainingPlanId,
            cancellationToken);
        if (plan is null)
            return Results.NotFound();

        var normalizedName = exerciseName!.Trim();
        var knownExercises = await progress.GetWorkoutExercisesAsync(
            plan.WorkoutName,
            cancellationToken);
        if (!knownExercises.Contains(normalizedName, StringComparer.Ordinal))
            return Results.NotFound();

        await progress.ClearExerciseProgressAsync(
            plan.WorkoutName,
            normalizedName,
            cancellationToken);
        return Results.NoContent();
    }

    private static ProgressSnapshotApiResponse ToResponse(
        ProgressSnapshot snapshot) =>
        new(snapshot.Date, snapshot.Score);

    private static ProgressChartPointApiResponse ToResponse(
        ProgressChartPoint point) =>
        new(point.Label, point.Percent);

    private static Dictionary<string, string[]> ValidateExerciseName(
        string? exerciseName)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(exerciseName) ||
            exerciseName.Trim().Length > 160)
        {
            errors[nameof(exerciseName)] =
                ["Exercise name is required and must not exceed 160 characters."];
        }

        return errors;
    }
}
