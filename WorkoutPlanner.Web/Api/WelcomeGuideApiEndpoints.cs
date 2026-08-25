using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;

namespace WorkoutPlanner.Web.Api;

public static class WelcomeGuideApiEndpoints
{
    public static RouteGroupBuilder MapWelcomeGuideApiEndpoints(this RouteGroupBuilder api)
    {
        var welcome = api.MapGroup("/welcome-guide")
            .WithTags("Welcome guide")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);

        welcome.MapGet("", GetStateAsync)
            .Produces<WelcomeGuideStateApiResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        welcome.MapPost("", CompleteAsync)
            .Produces<WelcomeGuideStateApiResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        return api;
    }

    private static async Task<IResult> GetStateAsync(IWelcomeGuideStateService state, CancellationToken cancellationToken) =>
        Results.Ok(new WelcomeGuideStateApiResponse(await state.IsCompletedAsync(cancellationToken)));

    private static async Task<IResult> CompleteAsync(IWelcomeGuideStateService state, CancellationToken cancellationToken)
    {
        await state.CompleteAsync(cancellationToken);
        return Results.Ok(new WelcomeGuideStateApiResponse(true));
    }
}
