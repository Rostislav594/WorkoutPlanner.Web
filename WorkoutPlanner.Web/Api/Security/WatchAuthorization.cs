using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.WearOs;

namespace WorkoutPlanner.Web.Api.Security;

public static class WatchAuthorization
{
    public const string DevicePolicyName = "WatchDevice";
    public const string ManagementPolicyName = "WatchManagement";
}

public sealed class WatchDeviceRequirement : IAuthorizationRequirement;
public sealed class WatchManagementRequirement : IAuthorizationRequirement;

public sealed class WatchDeviceAuthorizationHandler(
    SignInManager<IdentityUser> signInManager,
    IDbContextFactory<WorkoutDbContext> dbFactory,
    TimeProvider timeProvider)
    : AuthorizationHandler<WatchDeviceRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        WatchDeviceRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            context.User.FindFirstValue(WatchTokenService.IdentityTypeClaimType) !=
                WatchTokenService.WatchIdentityType ||
            !Guid.TryParse(
                context.User.FindFirstValue(WatchTokenService.DeviceIdClaimType),
                out var deviceId))
            return;

        var user = await signInManager.ValidateSecurityStampAsync(context.User);
        if (user is null)
            return;

        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.WatchDevices.AsNoTracking().AnyAsync(
                x => x.Id == deviceId &&
                     x.UserId == user.Id &&
                     x.RevokedAtUtc == null &&
                     x.RefreshTokenExpiresAtUtc > now,
                cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}

public sealed class WatchManagementAuthorizationHandler(
    SignInManager<IdentityUser> signInManager,
    MobileSessionService mobileSessions)
    : AuthorizationHandler<WatchManagementRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        WatchManagementRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            context.User.HasClaim(
                WatchTokenService.IdentityTypeClaimType,
                WatchTokenService.WatchIdentityType))
            return;

        var user = await signInManager.ValidateSecurityStampAsync(context.User);
        if (user is null)
            return;

        if (!MobileSessionService.TryGetSessionId(context.User, out _))
        {
            context.Succeed(requirement);
            return;
        }

        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;
        if (await mobileSessions.IsActiveAsync(
                context.User,
                user.Id,
                cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
