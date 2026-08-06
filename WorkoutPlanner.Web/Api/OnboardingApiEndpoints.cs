using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Api;

public static class OnboardingApiEndpoints
{
    public static RouteGroupBuilder MapOnboardingApiEndpoints(
        this RouteGroupBuilder api)
    {
        var onboarding = api.MapGroup("/onboarding")
            .WithTags("Onboarding")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);

        onboarding.MapGet("", GetStateAsync)
            .Produces<OnboardingStateApiResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        onboarding.MapPut("/progress", SaveProgressAsync)
            .Produces<OnboardingStateApiResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        onboarding.MapPost("/complete", CompleteAsync)
            .Produces<OnboardingStateApiResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        onboarding.MapDelete("", ResetAsync)
            .Produces<OnboardingStateApiResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return api;
    }

    private static async Task<IResult> GetStateAsync(
        IOnboardingStateService onboarding,
        CancellationToken cancellationToken) =>
        Results.Ok(ToResponse(await onboarding.GetStateAsync(cancellationToken)));

    private static async Task<IResult> SaveProgressAsync(
        SaveOnboardingProgressRequest request,
        IOnboardingStateService onboarding,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StepId) ||
            request.StepId.Trim().Length > 120)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.StepId)] =
                    ["A valid onboarding step identifier is required."]
            });
        }

        var result = await onboarding.SaveProgressAsync(
            request.StepId.Trim(),
            request.Outcome,
            cancellationToken);
        return ToMutationResult(result);
    }

    private static async Task<IResult> CompleteAsync(
        CompleteOnboardingRequest request,
        IOnboardingStateService onboarding,
        CancellationToken cancellationToken)
    {
        var result = await onboarding.CompleteAsync(
            request.Outcome,
            cancellationToken);
        return ToMutationResult(result);
    }

    private static async Task<IResult> ResetAsync(
        IOnboardingStateService onboarding,
        CancellationToken cancellationToken) =>
        Results.Ok(ToResponse(await onboarding.ResetAsync(cancellationToken)));

    private static IResult ToMutationResult(OnboardingMutationResult result)
    {
        if (result.Succeeded)
            return Results.Ok(ToResponse(result.State));

        return result.Failure switch
        {
            OnboardingMutationFailure.InvalidStep =>
                Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(SaveOnboardingProgressRequest.StepId)] =
                        ["The onboarding step is not part of the current guide."]
                }),
            OnboardingMutationFailure.InvalidOutcome =>
                Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(SaveOnboardingProgressRequest.Outcome)] =
                        ["The onboarding outcome is invalid."]
                }),
            _ => Results.Problem(
                title: "Onboarding is already completed.",
                statusCode: StatusCodes.Status409Conflict)
        };
    }

    private static OnboardingStateApiResponse ToResponse(
        OnboardingState state) =>
        new(
            state.IsCompleted,
            state.StepId,
            state.Outcome,
            state.UpdatedAtUtc);
}
