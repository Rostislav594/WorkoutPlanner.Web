using System.Security.Claims;

namespace WorkoutPlanner.Web.Services.Activity;

public sealed class UserActivityMiddleware(
    RequestDelegate next,
    ILogger<UserActivityMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        UserActivityService activities)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1") &&
            context.User.Identity?.IsAuthenticated == true &&
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } userId)
        {
            try
            {
                await activities.RecordSeenAsync(
                    userId,
                    UserActivityMetadata.FromHeaders(context.Request.Headers),
                    context.RequestAborted);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException ||
                !context.RequestAborted.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "Could not update activity for user {UserId}.",
                    userId);
            }
        }

        await next(context);
    }
}
