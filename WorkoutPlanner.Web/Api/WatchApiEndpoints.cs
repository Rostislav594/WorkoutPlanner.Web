using System.Security.Claims;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Services.WearOs;

namespace WorkoutPlanner.Web.Api;

public static class WatchApiEndpoints
{
    public const string PairingRateLimitPolicy = "WatchPairing";
    public const string PairingCodeRateLimitPolicy = "WatchPairingCode";
    public const string RefreshRateLimitPolicy = "WatchTokenRefresh";

    public static RouteGroupBuilder MapWatchApi(this IEndpointRouteBuilder endpoints)
    {
        var watch = endpoints.MapGroup("/api/watch")
            .WithTags("Wear OS");

        watch.MapPost("/pairing-codes", CreatePairingCodeAsync)
            .RequireAuthorization(WatchAuthorization.ManagementPolicyName)
            .RequireRateLimiting(PairingCodeRateLimitPolicy)
            .Produces<CreateWatchPairingCodeResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status429TooManyRequests);

        watch.MapPost("/pair", PairAsync)
            .AllowAnonymous()
            .RequireRateLimiting(PairingRateLimitPolicy)
            .Produces<WatchTokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .Produces(StatusCodes.Status429TooManyRequests);

        watch.MapPost("/token/refresh", RefreshAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RefreshRateLimitPolicy)
            .Produces<WatchTokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        watch.MapGet("/devices", GetDevicesAsync)
            .RequireAuthorization(WatchAuthorization.ManagementPolicyName)
            .Produces<IReadOnlyList<WatchDeviceResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        watch.MapDelete("/devices/{deviceId}", RevokeDeviceAsync)
            .RequireAuthorization(WatchAuthorization.ManagementPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        watch.MapGet("/workouts/active", GetActiveWorkoutAsync)
            .RequireAuthorization(WatchAuthorization.DevicePolicyName)
            .Produces<WatchActiveWorkoutResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        watch.MapPost("/sets/{setId:int}/complete", CompleteSetAsync)
            .RequireAuthorization(WatchAuthorization.DevicePolicyName)
            .Produces<CompleteWatchSetResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<WatchSetConflictResponse>(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return watch;
    }

    private static async Task<IResult> CreatePairingCodeAsync(
        ClaimsPrincipal principal,
        WatchPairingService pairing,
        CancellationToken cancellationToken)
    {
        var response = await pairing.CreateCodeAsync(
            GetRequiredUserId(principal),
            cancellationToken);
        return Results.Created("/api/watch/pairing-codes", response);
    }

    private static async Task<IResult> PairAsync(
        PairWatchRequest request,
        WatchPairingService pairing,
        CancellationToken cancellationToken)
    {
        var errors = ValidatePairRequest(request);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await pairing.PairAsync(request, cancellationToken);
        if (result.Succeeded)
            return Results.Ok(result.Tokens);

        return result.Failure switch
        {
            PairWatchFailure.ExpiredCode => Problem(
                StatusCodes.Status410Gone,
                "WATCH_PAIRING_CODE_EXPIRED",
                "The pairing code has expired."),
            PairWatchFailure.DeviceAlreadyPaired => Problem(
                StatusCodes.Status409Conflict,
                "WATCH_DEVICE_ALREADY_PAIRED",
                "The device is already paired with another account."),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                "WATCH_PAIRING_CODE_INVALID",
                "The pairing code is invalid.")
        };
    }

    private static async Task<IResult> RefreshAsync(
        RefreshWatchTokenRequest request,
        WatchPairingService pairing,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.RefreshToken)] = ["Refresh token is required."]
            });
        }

        var result = await pairing.RefreshAsync(
            request.RefreshToken,
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Tokens)
            : Problem(
                StatusCodes.Status401Unauthorized,
                "WATCH_REFRESH_TOKEN_INVALID",
                "The watch refresh token is invalid or expired.");
    }

    private static async Task<IResult> GetDevicesAsync(
        ClaimsPrincipal principal,
        WatchPairingService pairing,
        CancellationToken cancellationToken) =>
        Results.Ok(await pairing.GetDevicesAsync(
            GetRequiredUserId(principal),
            cancellationToken));

    private static async Task<IResult> RevokeDeviceAsync(
        string deviceId,
        ClaimsPrincipal principal,
        WatchPairingService pairing,
        CancellationToken cancellationToken) =>
        await pairing.RevokeAsync(
            GetRequiredUserId(principal),
            deviceId,
            cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task<IResult> GetActiveWorkoutAsync(
        IWatchWorkoutService workouts,
        CancellationToken cancellationToken)
    {
        var result = await workouts.GetActiveWorkoutAsync(cancellationToken);
        if (result.Availability == WatchWorkoutAvailability.Finished)
        {
            return Problem(
                StatusCodes.Status410Gone,
                "WORKOUT_ALREADY_FINISHED",
                "Today's workout has already been completed.");
        }

        if (result.Workout is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                "NO_ACTIVE_WORKOUT",
                "There is no active workout for today.");
        }

        return Results.Ok(ToResponse(result.Workout));
    }

    private static async Task<IResult> CompleteSetAsync(
        int setId,
        CompleteWatchSetRequest request,
        ClaimsPrincipal principal,
        IWatchWorkoutService workouts,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.OperationId == Guid.Empty)
            errors[nameof(request.OperationId)] = ["Operation ID is required."];
        if (request.ChangedAtUtc == default)
            errors[nameof(request.ChangedAtUtc)] = ["ChangedAtUtc is required."];
        if (request.ClientVersion < 0)
            errors[nameof(request.ClientVersion)] = ["Client version cannot be negative."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var deviceId = GetRequiredWatchDeviceId(principal);
        var result = await workouts.CompleteSetAsync(
            deviceId,
            setId,
            request.OperationId,
            request.ClientVersion,
            request.ChangedAtUtc,
            cancellationToken);
        if (result.Succeeded)
        {
            return Results.Ok(new CompleteWatchSetResponse(
                ToResponse(result.Set!),
                result.CurrentExerciseId,
                result.CurrentSetId,
                result.ProcessedAtUtc!.Value));
        }

        if (result.Failure == CompleteWatchSetFailure.Conflict)
        {
            return Results.Json(
                new WatchSetConflictResponse(
                    "https://gplanner.app/problems/workout-set-conflict",
                    "Workout set conflict",
                    StatusCodes.Status409Conflict,
                    "WORKOUT_SET_CONFLICT",
                    "The set changed after the watch loaded it.",
                    result.Set is null ? null : ToResponse(result.Set),
                    result.CurrentExerciseId,
                    result.CurrentSetId),
                statusCode: StatusCodes.Status409Conflict,
                contentType: "application/problem+json");
        }

        return result.Failure switch
        {
            CompleteWatchSetFailure.WorkoutFinished => Problem(
                StatusCodes.Status410Gone,
                "WORKOUT_ALREADY_FINISHED",
                "Today's workout has already been completed."),
            CompleteWatchSetFailure.OperationIdConflict => Problem(
                StatusCodes.Status409Conflict,
                "WATCH_OPERATION_ID_CONFLICT",
                "The operation ID was already used for another mutation."),
            _ => Problem(
                StatusCodes.Status404NotFound,
                "WATCH_SET_NOT_FOUND",
                "The set does not belong to the active workout.")
        };
    }

    private static Dictionary<string, string[]> ValidatePairRequest(
        PairWatchRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Code is null ||
            request.Code.Length != 6 ||
            request.Code.Any(x => !char.IsAsciiDigit(x)))
            errors[nameof(request.Code)] = ["Pairing code must contain exactly six digits."];
        if (string.IsNullOrWhiteSpace(request.DeviceId) || request.DeviceId.Trim().Length > 160)
            errors[nameof(request.DeviceId)] = ["Device ID is required and must not exceed 160 characters."];
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120)
            errors[nameof(request.DisplayName)] = ["Display name is required and must not exceed 120 characters."];
        if (request.DeviceModel?.Trim().Length > 120)
            errors[nameof(request.DeviceModel)] = ["Device model must not exceed 120 characters."];
        if (request.AppVersion?.Trim().Length > 40)
            errors[nameof(request.AppVersion)] = ["App version must not exceed 40 characters."];
        return errors;
    }

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(
            statusCode: status,
            title: code,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private static WatchActiveWorkoutResponse ToResponse(ActiveWorkout workout)
    {
        var exercises = workout.Exercises
            .Select(x => new WatchExerciseResponse(
                x.ExerciseId,
                x.Name,
                x.Order,
                x.Sets.Select(ToResponse).ToList()))
            .ToList();
        var current = exercises
            .SelectMany(exercise => exercise.Sets
                .Where(set => !set.IsCompleted)
                .Select(set => (exercise.ExerciseId, set.SetId)))
            .FirstOrDefault();
        return new WatchActiveWorkoutResponse(
            workout.WorkoutId,
            workout.WorkoutName,
            workout.ScheduledDate,
            StartedAtUtc: null,
            exercises,
            current == default ? null : current.ExerciseId,
            current == default ? null : current.SetId);
    }

    private static WatchSetResponse ToResponse(ActiveWorkoutSet set) =>
        new(
            set.SetId,
            set.SetNumber,
            set.Weight,
            set.Repetitions,
            set.Completed,
            set.IsWarmup,
            set.Version);

    private static Guid GetRequiredWatchDeviceId(ClaimsPrincipal principal) =>
        Guid.TryParse(
            principal.FindFirstValue(WatchTokenService.DeviceIdClaimType),
            out var deviceId)
            ? deviceId
            : throw new InvalidOperationException(
                "Authenticated watch principal has no device identifier.");

    private static string GetRequiredUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException(
            "Authenticated principal has no user identifier.");
}
