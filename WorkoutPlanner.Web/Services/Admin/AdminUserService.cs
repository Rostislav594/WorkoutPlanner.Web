using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Activity;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services.Admin;

public sealed class AdminUserService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    AdminAccessVerifier access,
    IOptions<UserActivityOptions> activityOptions,
    TimeProvider timeProvider,
    IPushNotificationService pushNotifications,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    private const int RecentTicketCount = 10;
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
                user.Email ?? user.UserName ?? string.Empty,
                user.LockoutEnd))
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
                ticketCounts.GetValueOrDefault(identity.UserId),
                IsBlocked(identity.LockoutEnd));
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
        var isAdmin = await IsAdminAsync(db, userId, cancellationToken);
        var tickets = await db.SupportTickets.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(RecentTicketCount)
            .Select(x => new { x.Id, x.TicketNumber, x.Message, x.UpdatedAtUtc, x.Status })
            .ToListAsync(cancellationToken);

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
            identity.LockoutEnd,
            IsBlocked(identity.LockoutEnd),
            isAdmin,
            tickets.Select(x => new AdminUserTicketItem(
                x.Id,
                x.TicketNumber ?? x.Id.ToString(),
                CreatePreview(x.Message, 120),
                x.UpdatedAtUtc,
                x.Status)).ToArray());
    }

    public async Task<bool> BlockAsync(string userId, CancellationToken cancellationToken = default)
    {
        var adminUserId = await access.GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
            return false;
        if (await IsAdminAsync(db, userId, cancellationToken))
            throw new InvalidOperationException("Administrators cannot be blocked.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await EndAllSessionsAsync(db, user, cancellationToken);
        AddAudit(db, adminUserId, AdminAuditActions.UserBlocked, userId);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnblockAsync(string userId, CancellationToken cancellationToken = default)
    {
        var adminUserId = await access.GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
            return false;

        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        AddAudit(db, adminUserId, AdminAuditActions.UserUnblocked, userId);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SignOutEverywhereAsync(string userId, CancellationToken cancellationToken = default)
    {
        var adminUserId = await access.GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
            return false;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await EndAllSessionsAsync(db, user, cancellationToken);
        AddAudit(db, adminUserId, AdminAuditActions.UserSignedOutEverywhere, userId);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<long?> SendMessageAsync(
        string userId,
        AdminUserMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var title = message.Title.Trim();
        var body = message.Body.Trim();
        if (title.Length is 0 or > 160)
            throw new InvalidDataException("Message title must contain between 1 and 160 characters.");
        if (body.Length is 0 or > 8000)
            throw new InvalidDataException("Message must contain between 1 and 8000 characters.");

        var adminUserId = await access.GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(x => x.Id == userId, cancellationToken))
            return null;

        var inboxMessage = new InboxMessage
        {
            UserId = userId,
            Type = InboxMessageType.System,
            Title = title,
            Preview = CreatePreview(body, 180),
            Body = body,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        db.InboxMessages.Add(inboxMessage);
        AddAudit(db, adminUserId, AdminAuditActions.UserMessageSent, userId);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await pushNotifications.NotifyInboxMessageAsync(
                userId,
                PushNotificationType.InboxMessage,
                inboxMessage.Id,
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Сообщение уже во «Входящих»; без push пользователь увидит его, когда откроет приложение.
            logger.LogWarning(
                exception,
                "Could not send push for admin message {InboxMessageId}.",
                inboxMessage.Id);
        }

        return inboxMessage.Id;
    }

    /// <summary>
    /// Новая метка безопасности обнуляет уже выданные токены телефона и часов,
    /// а отозванные сессии и устройства не дают их обновить.
    /// </summary>
    private async Task EndAllSessionsAsync(
        WorkoutDbContext db,
        IdentityUser user,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.SecurityStamp = Guid.NewGuid().ToString("N").ToUpperInvariant();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await db.MobileSessions
            .Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), cancellationToken);
        await db.WatchDevices
            .Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), cancellationToken);
        await db.PushDeviceRegistrations
            .Where(x => x.UserId == user.Id && x.IsActive)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.UpdatedAtUtc, now),
                cancellationToken);
    }

    private void AddAudit(WorkoutDbContext db, string adminUserId, string action, string userId) =>
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = adminUserId,
            Action = action,
            TargetType = AdminAuditTargets.User,
            TargetId = userId,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });

    private static Task<bool> IsAdminAsync(
        WorkoutDbContext db,
        string userId,
        CancellationToken cancellationToken) =>
        db.UserRoles.AnyAsync(
            link => link.UserId == userId &&
                    db.Roles.Any(role => role.Id == link.RoleId && role.Name == ApplicationRoles.Admin),
            cancellationToken);

    // Блокировка из админки бессрочная; временная — после неверных паролей — сюда не входит.
    private static bool IsBlocked(DateTimeOffset? lockoutEnd) =>
        lockoutEnd is { } end && end.Year >= 9999;

    private static string CreatePreview(string text, int length) =>
        text.Length <= length ? text : $"{text[..(length - 1)]}…";

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

    private sealed record IdentityRow(string UserId, string Email, DateTimeOffset? LockoutEnd);
    private sealed record IdentityDetailRow(
        string UserId,
        string Email,
        DateTimeOffset? LockoutEnd);
    private sealed record ProfileRow(string UserId, string FirstName, string LastName);
    private sealed record CountRow(string UserId, long Count);
}
