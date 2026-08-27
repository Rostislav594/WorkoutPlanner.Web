using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Activity;

namespace WorkoutPlanner.Web.Services.Admin;

public sealed class AdminUserService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    AdminAccessVerifier access,
    IOptions<UserActivityOptions> activityOptions,
    TimeProvider timeProvider) : IAdminUserService
{
    private readonly UserActivityOptions _activityOptions = activityOptions.Value;

    public async Task<AdminPagedResult<AdminUserListItem>> GetPageAsync(
        AdminUserListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await access.GetRequiredAdminUserIdAsync();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var search = query.Search?.Trim();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToUpperInvariant();
            var lowered = search.ToLowerInvariant();
            users = users.Where(user =>
                user.Id.Contains(search) ||
                (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalized)) ||
                (user.NormalizedUserName != null && user.NormalizedUserName.Contains(normalized)) ||
                db.UserProfiles.Any(profile =>
                    profile.UserId == user.Id &&
                    (profile.FirstName.ToLower().Contains(lowered) ||
                     profile.LastName.ToLower().Contains(lowered))));
        }

        var total = await users.LongCountAsync(cancellationToken);
        var identityRows = await users
            .OrderBy(user => user.NormalizedEmail ?? user.NormalizedUserName ?? user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new IdentityRow(
                user.Id,
                user.Email ?? user.UserName ?? string.Empty))
            .ToListAsync(cancellationToken);
        if (identityRows.Count == 0)
            return new AdminPagedResult<AdminUserListItem>([], page, pageSize, total);

        var userIds = identityRows.Select(x => x.UserId).ToArray();
        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new ProfileRow(x.UserId, x.FirstName, x.LastName))
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        var activities = await db.Set<UserActivity>().AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        var workoutCounts = await db.WorkoutHistory.AsNoTracking()
            .Where(x => x.UserId != null && userIds.Contains(x.UserId))
            .GroupBy(x => x.UserId!)
            .Select(group => new CountRow(group.Key, group.LongCount()))
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);
        var ticketCounts = await db.SupportTickets.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .GroupBy(x => x.UserId)
            .Select(group => new CountRow(group.Key, group.LongCount()))
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var items = identityRows.Select(identity =>
        {
            profiles.TryGetValue(identity.UserId, out var profile);
            activities.TryGetValue(identity.UserId, out var activity);
            return new AdminUserListItem(
                identity.UserId,
                GetDisplayName(profile, identity.Email),
                identity.Email,
                activity?.RegisteredAtUtc,
                activity?.LastSeenAtUtc,
                GetActivityStatus(activity?.LastSeenAtUtc, now),
                activity?.Platform,
                activity?.AppVersion,
                workoutCounts.GetValueOrDefault(identity.UserId),
                ticketCounts.GetValueOrDefault(identity.UserId));
        }).ToArray();

        return new AdminPagedResult<AdminUserListItem>(items, page, pageSize, total);
    }

    public async Task<AdminUserDetail?> GetAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;
        await access.GetRequiredAdminUserIdAsync();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var identity = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new IdentityDetailRow(
                x.Id,
                x.Email ?? x.UserName ?? string.Empty,
                x.EmailConfirmed,
                x.TwoFactorEnabled,
                x.LockoutEnd))
            .SingleOrDefaultAsync(cancellationToken);
        if (identity is null)
            return null;

        var profile = await db.UserProfiles.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new ProfileRow(x.UserId, x.FirstName, x.LastName))
            .SingleOrDefaultAsync(cancellationToken);
        var activity = await db.Set<UserActivity>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var workoutCount = await db.WorkoutHistory.LongCountAsync(
            x => x.UserId == userId,
            cancellationToken);
        var ticketCount = await db.SupportTickets.LongCountAsync(
            x => x.UserId == userId,
            cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var sessionCount = await db.MobileSessions.LongCountAsync(
            x => x.UserId == userId &&
                 x.RevokedAtUtc == null &&
                 x.ExpiresAtUtc > now,
            cancellationToken);

        return new AdminUserDetail(
            identity.UserId,
            GetDisplayName(profile, identity.Email),
            identity.Email,
            activity?.RegisteredAtUtc,
            activity?.LastSeenAtUtc,
            GetActivityStatus(activity?.LastSeenAtUtc, now),
            activity?.Platform,
            activity?.AppVersion,
            activity?.OsVersion,
            activity?.DeviceModel,
            workoutCount,
            ticketCount,
            sessionCount,
            identity.EmailConfirmed,
            identity.TwoFactorEnabled,
            identity.LockoutEnd);
    }

    private AdminUserActivityStatus GetActivityStatus(DateTime? lastSeenAtUtc, DateTime now)
    {
        if (lastSeenAtUtc is null)
            return AdminUserActivityStatus.NeverActive;
        if (lastSeenAtUtc >= now - PositiveWindow(_activityOptions.ActiveWindow, TimeSpan.FromDays(7)))
            return AdminUserActivityStatus.Active;
        if (lastSeenAtUtc >= now - PositiveWindow(_activityOptions.DormantWindow, TimeSpan.FromDays(30)))
            return AdminUserActivityStatus.Dormant;
        return AdminUserActivityStatus.Inactive;
    }

    private static TimeSpan PositiveWindow(TimeSpan configured, TimeSpan fallback) =>
        configured > TimeSpan.Zero ? configured : fallback;

    private static string GetDisplayName(ProfileRow? profile, string fallback)
    {
        if (profile is null)
            return fallback;
        var name = $"{profile.FirstName} {profile.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private sealed record IdentityRow(string UserId, string Email);
    private sealed record IdentityDetailRow(
        string UserId,
        string Email,
        bool EmailConfirmed,
        bool TwoFactorEnabled,
        DateTimeOffset? LockoutEnd);
    private sealed record ProfileRow(string UserId, string FirstName, string LastName);
    private sealed record CountRow(string UserId, long Count);
}
