using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace WorkoutPlanner.Web.Api.Security;

public static class MobileApiAuthorization
{
    public const string PolicyName = "MobileApiSession";
}

public sealed class MobileApiSessionRequirement : IAuthorizationRequirement;

public sealed class MobileApiSessionAuthorizationHandler(
    SignInManager<IdentityUser> signInManager,
    MobileSessionService mobileSessions)
    : AuthorizationHandler<MobileApiSessionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MobileApiSessionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        var user = await signInManager.ValidateSecurityStampAsync(context.User);
        if (user is null)
            return;

        if (await mobileSessions.IsActiveAsync(
                context.User,
                user.Id,
                cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
