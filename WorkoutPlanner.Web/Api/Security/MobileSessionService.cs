using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Api.Security;

public sealed class MobileSessionService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    TimeProvider timeProvider)
{
    public const string SessionIdClaimType = "gymplanner:mobile_session";

    public async Task<MobileSession> CreateAsync(
        string userId,
        string? deviceName,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var session = new MobileSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceName = NormalizeDeviceName(deviceName),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            ExpiresAtUtc = expiresAtUtc
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.MobileSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<bool> IsActiveAsync(
        ClaimsPrincipal principal,
        string userId,
        CancellationToken cancellationToken)
    {
        if (!TryGetSessionId(principal, out var sessionId))
            return false;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.MobileSessions.AsNoTracking().AnyAsync(
            x => x.Id == sessionId &&
                 x.UserId == userId &&
                 x.RevokedAtUtc == null &&
                 x.ExpiresAtUtc > now,
            cancellationToken);
    }

    public async Task<bool> RefreshAsync(
        ClaimsPrincipal principal,
        string userId,
        CancellationToken cancellationToken)
    {
        if (!TryGetSessionId(principal, out var sessionId))
            return false;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.MobileSessions.FirstOrDefaultAsync(
            x => x.Id == sessionId &&
                 x.UserId == userId &&
                 x.RevokedAtUtc == null &&
                 x.ExpiresAtUtc > now,
            cancellationToken);
        if (session is null)
            return false;

        session.LastRefreshedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RevokeCurrentAsync(
        ClaimsPrincipal principal,
        string userId,
        CancellationToken cancellationToken)
    {
        if (!TryGetSessionId(principal, out var sessionId))
            return;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.MobileSessions.FirstOrDefaultAsync(
            x => x.Id == sessionId &&
                 x.UserId == userId &&
                 x.RevokedAtUtc == null,
            cancellationToken);
        if (session is null)
            return;

        session.RevokedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.MobileSessions
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.RevokedAtUtc, now),
                cancellationToken);
    }

    public static void AddSessionClaim(
        ClaimsPrincipal principal,
        Guid sessionId)
    {
        if (principal.Identity is not ClaimsIdentity identity)
            throw new InvalidOperationException("Identity principal is not writable.");

        identity.AddClaim(new Claim(
            SessionIdClaimType,
            sessionId.ToString("D")));
    }

    public static bool TryGetSessionId(
        ClaimsPrincipal principal,
        out Guid sessionId) =>
        Guid.TryParse(
            principal.FindFirstValue(SessionIdClaimType),
            out sessionId);

    private static string? NormalizeDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return null;

        var normalized = deviceName.Trim();
        return normalized.Length <= 120
            ? normalized
            : normalized[..120];
    }
}
