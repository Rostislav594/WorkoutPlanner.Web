using System.Security.Claims;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Localization;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Services.WearOs;

namespace WorkoutPlanner.Web.Api;

public static class WatchApiEndpoints
{
    public const string PairingRateLimitPolicy = "WatchPairing";
    public const string PairingCodeRateLimitPolicy = "WatchPairingCode";

    /// <summary>Опрос статуса заявки идёт чаще остального: часы ждут подтверждения.</summary>
    public const string PairingStatusRateLimitPolicy = "WatchPairingStatus";
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

        // --- Сопряжение подтверждением на телефоне ---

        watch.MapPost("/pair/request", StartPairingRequestAsync)
            .AllowAnonymous()
            .RequireRateLimiting(PairingRateLimitPolicy)
            .Produces<StartWatchPairingResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests);

        watch.MapPost("/pair/status", PollPairingRequestAsync)
            .AllowAnonymous()
            .RequireRateLimiting(PairingStatusRateLimitPolicy)
            .Produces<WatchPairingStatusResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        watch.MapGet("/pair/requests/{requestId}", GetPairingRequestAsync)
            .RequireAuthorization(WatchAuthorization.ManagementPolicyName)
            .Produces<WatchPairingRequestDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        watch.MapPost("/pair/requests/{requestId}/approve", ApprovePairingRequestAsync)
            .RequireAuthorization(WatchAuthorization.ManagementPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        watch.MapPost("/pair/requests/{requestId}/reject", RejectPairingRequestAsync)
            .RequireAuthorization(WatchAuthorization.ManagementPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

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

        watch.MapPut("/devices/{deviceId}", RenameDeviceAsync)
            .RequireAuthorization(WatchAuthorization.ManagementPolicyName)
            .Produces<WatchDeviceResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        watch.MapDelete("/devices/{deviceId}", RevokeDeviceAsync)
            .RequireAuthorization(WatchAuthorization.ManagementPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        watch.MapDelete("/devices", RevokeAllDevicesAsync)
            .RequireAuthorization(WatchAuthorization.ManagementPolicyName)
            .Produces(StatusCodes.Status204NoContent)
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

        watch.MapPut("/sets/{setId:int}", UpdateSetAsync)
            .RequireAuthorization(WatchAuthorization.DevicePolicyName)
            .Produces<WatchSetMutationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<WatchSetConflictResponse>(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        watch.MapPost("/sets/{setId:int}/undo", UndoSetAsync)
            .RequireAuthorization(WatchAuthorization.DevicePolicyName)
            .Produces<WatchSetMutationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<WatchSetConflictResponse>(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        watch.MapPost("/workouts/{workoutId:int}/finish", FinishWorkoutAsync)
            .RequireAuthorization(WatchAuthorization.DevicePolicyName)
            .Produces<FinishWatchWorkoutResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
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

    private static async Task<IResult> StartPairingRequestAsync(
        StartWatchPairingRequest request,
        WatchPairingService pairing,
        CancellationToken cancellationToken)
    {
        var errors = ValidateDeviceMetadata(
            request.DeviceId,
            request.DisplayName,
            request.DeviceModel,
            request.AppVersion);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var response = await pairing.StartRequestAsync(request, cancellationToken);
        return Results.Created("/api/watch/pair/request", response);
    }

    private static async Task<IResult> PollPairingRequestAsync(
        WatchPairingStatusRequest request,
        WatchPairingService pairing,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.RequestId))
            errors[nameof(request.RequestId)] = ["Request ID is required."];
        if (string.IsNullOrWhiteSpace(request.PollToken))
            errors[nameof(request.PollToken)] = ["Poll token is required."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await pairing.PollRequestAsync(
            request.RequestId,
            request.PollToken,
            cancellationToken);
        if (result.Succeeded)
            return Results.Ok(new WatchPairingStatusResponse(result.Status!, result.Tokens));

        return result.Failure switch
        {
            WatchPairingRequestFailure.DeviceOwnedByAnotherUser => Problem(
                StatusCodes.Status409Conflict,
                "WATCH_DEVICE_ALREADY_PAIRED",
                "The device is already paired with another account."),
            _ => Problem(
                StatusCodes.Status404NotFound,
                "WATCH_PAIRING_REQUEST_NOT_FOUND",
                "The pairing request does not exist.")
        };
    }

    private static async Task<IResult> GetPairingRequestAsync(
        string requestId,
        WatchPairingService pairing,
        CancellationToken cancellationToken)
    {
        var details = await pairing.GetRequestAsync(requestId, cancellationToken);
        return details is null
            ? ApiProblems.Problem(
                ApiErrorCodes.WatchPairingRequestNotFound,
                "The pairing request does not exist.",
                StatusCodes.Status404NotFound)
            : Results.Ok(details);
    }

    private static async Task<IResult> ApprovePairingRequestAsync(
        string requestId,
        ClaimsPrincipal principal,
        WatchPairingService pairing,
        CancellationToken cancellationToken) =>
        ToDecisionResult(await pairing.ApproveRequestAsync(
            GetRequiredUserId(principal),
            requestId,
            cancellationToken));

    private static async Task<IResult> RejectPairingRequestAsync(
        string requestId,
        ClaimsPrincipal principal,
        WatchPairingService pairing,
        CancellationToken cancellationToken) =>
        ToDecisionResult(await pairing.RejectRequestAsync(
            GetRequiredUserId(principal),
            requestId,
            cancellationToken));

    private static IResult ToDecisionResult(WatchPairingDecisionResult result)
    {
        if (result.Succeeded)
            return Results.NoContent();

        return result.Failure switch
        {
            WatchPairingRequestFailure.Expired => ApiProblems.Problem(
                ApiErrorCodes.WatchPairingRequestExpired,
                "The pairing request has expired.",
                StatusCodes.Status410Gone),
            WatchPairingRequestFailure.AlreadyResolved => ApiProblems.Problem(
                ApiErrorCodes.WatchPairingRequestAlreadyResolved,
                "The pairing request has already been answered.",
                StatusCodes.Status409Conflict),
            WatchPairingRequestFailure.DeviceOwnedByAnotherUser => ApiProblems.Problem(
                ApiErrorCodes.WatchDeviceAlreadyPaired,
                "The device is already paired with another account.",
                StatusCodes.Status409Conflict),
            _ => ApiProblems.Problem(
                ApiErrorCodes.WatchPairingRequestNotFound,
                "The pairing request does not exist.",
                StatusCodes.Status404NotFound)
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

    private static async Task<IResult> RenameDeviceAsync(
        string deviceId,
        RenameWatchDeviceRequest request,
        ClaimsPrincipal principal,
        WatchPairingService pairing,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName) ||
            request.DisplayName.Trim().Length > 120)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.DisplayName)] =
                    ["Display name is required and must not exceed 120 characters."]
            });
        }

        var device = await pairing.RenameAsync(
            GetRequiredUserId(principal),
            deviceId,
            request.DisplayName,
            cancellationToken);
        return device is null
            ? Results.NotFound()
            : Results.Ok(device);
    }

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

    private static async Task<IResult> RevokeAllDevicesAsync(
        ClaimsPrincipal principal,
        WatchPairingService pairing,
        CancellationToken cancellationToken)
    {
        await pairing.RevokeAllAsync(
            GetRequiredUserId(principal),
            cancellationToken);
        return Results.NoContent();
    }

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

        return Results.Ok(ToResponse(result));
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
        return ToSetMutationResult(
            result,
            static (set, currentExerciseId, currentSetId, processedAtUtc) =>
                new CompleteWatchSetResponse(
                    set,
                    currentExerciseId,
                    currentSetId,
                    processedAtUtc));
    }

    private static async Task<IResult> UpdateSetAsync(
        int setId,
        UpdateWatchSetRequest request,
        ClaimsPrincipal principal,
        IWatchWorkoutService workouts,
        CancellationToken cancellationToken)
    {
        var errors = ValidateMutationRequest(
            request.OperationId,
            request.ChangedAtUtc,
            request.ClientVersion);
        if (!double.IsFinite(request.ActualWeight) ||
            request.ActualWeight is < 0 or > 2000)
        {
            errors[nameof(request.ActualWeight)] =
                ["Actual weight must be between 0 and 2000."];
        }
        if (request.ActualReps is < 1 or > 1000)
        {
            errors[nameof(request.ActualReps)] =
                ["Actual repetitions must be between 1 and 1000."];
        }
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await workouts.UpdateSetAsync(
            GetRequiredWatchDeviceId(principal),
            setId,
            request.OperationId,
            request.ActualWeight,
            request.ActualReps,
            request.ClientVersion,
            request.ChangedAtUtc,
            cancellationToken);
        return ToSetMutationResult(
            result,
            static (set, currentExerciseId, currentSetId, processedAtUtc) =>
                new WatchSetMutationResponse(
                    set,
                    currentExerciseId,
                    currentSetId,
                    processedAtUtc));
    }

    private static async Task<IResult> UndoSetAsync(
        int setId,
        UndoWatchSetRequest request,
        ClaimsPrincipal principal,
        IWatchWorkoutService workouts,
        CancellationToken cancellationToken)
    {
        var errors = ValidateMutationRequest(
            request.OperationId,
            request.ChangedAtUtc,
            request.ClientVersion);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await workouts.UndoSetAsync(
            GetRequiredWatchDeviceId(principal),
            setId,
            request.OperationId,
            request.ClientVersion,
            request.ChangedAtUtc,
            cancellationToken);
        return ToSetMutationResult(
            result,
            static (set, currentExerciseId, currentSetId, processedAtUtc) =>
                new WatchSetMutationResponse(
                    set,
                    currentExerciseId,
                    currentSetId,
                    processedAtUtc));
    }

    private static async Task<IResult> FinishWorkoutAsync(
        int workoutId,
        ClaimsPrincipal principal,
        IWatchWorkoutService workouts,
        CancellationToken cancellationToken)
    {
        var result = await workouts.FinishWorkoutAsync(
            GetRequiredWatchDeviceId(principal),
            workoutId,
            cancellationToken);
        if (result.Succeeded)
            return Results.Ok(new FinishWatchWorkoutResponse(workoutId, result.AlreadyFinished));

        return result.Failure switch
        {
            FinishWatchWorkoutFailure.WorkoutNotReady => Problem(
                StatusCodes.Status409Conflict,
                "WORKOUT_NOT_READY_TO_FINISH",
                "All workout sets must be completed before finishing from the watch."),
            _ => Problem(
                StatusCodes.Status404NotFound,
                "WATCH_WORKOUT_NOT_FOUND",
                "The workout does not belong to the active watch user.")
        };
    }

    private static Dictionary<string, string[]> ValidateMutationRequest(
        Guid operationId,
        DateTime changedAtUtc,
        long clientVersion)
    {
        var errors = new Dictionary<string, string[]>();
        if (operationId == Guid.Empty)
            errors[nameof(operationId)] = ["Operation ID is required."];
        if (changedAtUtc == default)
            errors[nameof(changedAtUtc)] = ["ChangedAtUtc is required."];
        if (clientVersion < 0)
            errors[nameof(clientVersion)] = ["Client version cannot be negative."];
        return errors;
    }

    private static IResult ToSetMutationResult<TResponse>(
        CompleteWatchSetResult result,
        Func<WatchSetResponse, int?, int?, DateTime, TResponse> createResponse)
    {
        if (result.Succeeded)
        {
            return Results.Ok(createResponse(
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
                    "The set changed after the watch loaded it or the requested state is no longer applicable.",
                    result.Set is null ? null : ToResponse(result.Set),
                    result.CurrentExerciseId,
                    result.CurrentSetId),
                statusCode: StatusCodes.Status409Conflict,
                contentType: "application/problem+json");
        }

        return result.Failure switch
        {
            CompleteWatchSetFailure.InvalidValues => Problem(
                StatusCodes.Status400BadRequest,
                "WATCH_SET_VALUES_INVALID",
                "The requested weight or repetitions are invalid."),
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
        var errors = ValidateDeviceMetadata(
            request.DeviceId,
            request.DisplayName,
            request.DeviceModel,
            request.AppVersion);
        if (request.Code is null ||
            request.Code.Length != 6 ||
            request.Code.Any(x => !char.IsAsciiDigit(x)))
            errors[nameof(request.Code)] = ["Pairing code must contain exactly six digits."];
        return errors;
    }

    /// <summary>Общие ограничения метаданных устройства для обоих способов сопряжения.</summary>
    private static Dictionary<string, string[]> ValidateDeviceMetadata(
        string? deviceId,
        string? displayName,
        string? deviceModel,
        string? appVersion)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Trim().Length > 160)
            errors["DeviceId"] = ["Device ID is required and must not exceed 160 characters."];
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 120)
            errors["DisplayName"] = ["Display name is required and must not exceed 120 characters."];
        if (deviceModel?.Trim().Length > 120)
            errors["DeviceModel"] = ["Device model must not exceed 120 characters."];
        if (appVersion?.Trim().Length > 40)
            errors["AppVersion"] = ["App version must not exceed 40 characters."];
        return errors;
    }

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(
            statusCode: status,
            title: code,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private static WatchActiveWorkoutResponse ToResponse(WatchActiveWorkoutResult result)
    {
        var workout = result.Workout!;
        var exercises = workout.Exercises
            .Select(x => new WatchExerciseResponse(
                x.ExerciseId,
                x.Name,
                x.Order,
                x.SupersetGroupId,
                x.Sets.Select(ToResponse).ToList()))
            .ToList();
        var current = OrderSetsForWatch(workout)
            .FirstOrDefault(x => !x.Set.Completed);
        return new WatchActiveWorkoutResponse(
            workout.WorkoutId,
            workout.WorkoutName,
            workout.ScheduledDate,
            StartedAtUtc: null,
            exercises,
            current is null ? null : current.ExerciseId,
            current is null ? null : current.Set.SetId,
            result.RestBetweenSetsSeconds,
            result.RestBetweenExercisesSeconds,
            FreeWorkoutDraftId.IsDraft(workout.WorkoutId));
    }

    /// <summary>
    /// Порядок, в котором часы ведут человека по тренировке.
    /// </summary>
    /// <remarks>
    /// Упражнения вне суперсета идут подряд: все подходы одного, потом другого.
    /// Внутри суперсета подходы чередуются кругами — первый подход каждого
    /// упражнения группы, затем второй и так далее. Простой перебор по порядку
    /// упражнений повёл бы человека неверно: суперсет тем и отличается, что
    /// упражнения выполняются вперемежку.
    /// </remarks>
    private static IEnumerable<WatchOrderedSet> OrderSetsForWatch(ActiveWorkout workout)
    {
        var groups = workout.Exercises
            .OrderBy(x => x.Order)
            .GroupBy(x => x.SupersetGroupId is { } groupId ? $"s:{groupId}" : $"e:{x.ExerciseId}")
            .Select(group => group.OrderBy(x => x.Order).ToList())
            .OrderBy(group => group[0].Order);

        foreach (var group in groups)
        {
            if (group.Count == 1)
            {
                foreach (var set in group[0].Sets.OrderBy(x => x.SetNumber))
                    yield return new WatchOrderedSet(group[0].ExerciseId, set);
                continue;
            }

            var rounds = group.Max(x => x.Sets.Count);
            for (var round = 0; round < rounds; round++)
            {
                foreach (var exercise in group)
                {
                    var ordered = exercise.Sets.OrderBy(x => x.SetNumber).ToList();
                    if (round < ordered.Count)
                        yield return new WatchOrderedSet(exercise.ExerciseId, ordered[round]);
                }
            }
        }
    }

    private sealed record WatchOrderedSet(int ExerciseId, ActiveWorkoutSet Set);

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
