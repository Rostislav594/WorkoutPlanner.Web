using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.Admin;

public sealed class AdminSupportService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    AdminAccessVerifier access,
    ISupportScreenshotStorage screenshots,
    TimeProvider timeProvider,
    IPushNotificationService pushNotifications,
    ILogger<AdminSupportService> logger) : IAdminSupportService
{
    public async Task<AdminPagedResult<AdminSupportListItem>> GetPageAsync(
        AdminSupportListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await access.GetRequiredAdminUserIdAsync();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var search = query.Search?.Trim();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var tickets = db.SupportTickets.AsNoTracking();
        if (query.Status is not null)
            tickets = tickets.Where(x => x.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToUpperInvariant();
            var lowered = search.ToLowerInvariant();
            tickets = tickets.Where(ticket =>
                (ticket.TicketNumber != null && ticket.TicketNumber.Contains(search)) ||
                ticket.UserId.Contains(search) ||
                db.Users.Any(user =>
                    user.Id == ticket.UserId &&
                    ((user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalized)) ||
                     (user.NormalizedUserName != null && user.NormalizedUserName.Contains(normalized)))) ||
                db.UserProfiles.Any(profile =>
                    profile.UserId == ticket.UserId &&
                    (profile.FirstName.ToLower().Contains(lowered) ||
                     profile.LastName.ToLower().Contains(lowered))));
        }

        var total = await tickets.LongCountAsync(cancellationToken);
        var rows = await tickets
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ticket => new TicketListRow(
                ticket.Id,
                ticket.TicketNumber ?? ticket.Id.ToString(),
                ticket.UserId,
                db.UserProfiles.Where(profile => profile.UserId == ticket.UserId)
                    .Select(profile => (profile.FirstName + " " + profile.LastName).Trim())
                    .FirstOrDefault(),
                db.Users.Where(user => user.Id == ticket.UserId)
                    .Select(user => user.Email ?? user.UserName ?? string.Empty)
                    .FirstOrDefault() ?? string.Empty,
                ticket.Message,
                ticket.CreatedAtUtc,
                ticket.UpdatedAtUtc,
                ticket.Status,
                ticket.ScreenshotPath != null))
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new AdminSupportListItem(
            row.Id,
            row.TicketNumber,
            row.UserId,
            string.IsNullOrWhiteSpace(row.DisplayName) ? row.Email : row.DisplayName,
            row.Email,
            CreatePreview(row.Message),
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.Status,
            row.HasScreenshot)).ToArray();
        return new AdminPagedResult<AdminSupportListItem>(items, page, pageSize, total);
    }

    public async Task<AdminSupportTicketDetail?> GetAsync(
        long ticketId,
        CancellationToken cancellationToken = default)
    {
        await access.GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await db.SupportTickets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null)
            return null;

        var email = await db.Users.AsNoTracking()
            .Where(x => x.Id == ticket.UserId)
            .Select(x => x.Email ?? x.UserName ?? string.Empty)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var displayName = await db.UserProfiles.AsNoTracking()
            .Where(x => x.UserId == ticket.UserId)
            .Select(x => (x.FirstName + " " + x.LastName).Trim())
            .SingleOrDefaultAsync(cancellationToken);
        var messages = await db.Set<SupportMessage>().AsNoTracking()
            .Where(x => x.SupportTicketId == ticketId)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new AdminSupportMessageItem(
                x.Id,
                x.SenderType,
                x.Message,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        if (messages.Count == 0)
        {
            messages.Add(new AdminSupportMessageItem(
                0,
                SupportMessageSenderType.User,
                ticket.Message,
                ticket.CreatedAtUtc));
        }

        return new AdminSupportTicketDetail(
            ticket.Id,
            ticket.TicketNumber ?? ticket.Id.ToString(CultureInfo.InvariantCulture),
            ticket.UserId,
            string.IsNullOrWhiteSpace(displayName) ? email : displayName,
            email,
            ticket.Message,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            ticket.Status,
            ticket.AppVersion,
            ticket.Platform,
            ticket.OsVersion,
            ticket.DeviceModel,
            !string.IsNullOrWhiteSpace(ticket.ScreenshotPath),
            messages);
    }

    public async Task<bool> ChangeStatusAsync(
        long ticketId,
        SupportTicketStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
            throw new InvalidDataException("Invalid support ticket status.");
        var adminUserId = await access.GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await db.SupportTickets.SingleOrDefaultAsync(
            x => x.Id == ticketId,
            cancellationToken);
        if (ticket is null)
            return false;
        if (ticket.Status == status)
            return true;

        ticket.Status = status;
        ticket.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        db.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminUserId = adminUserId,
            Action = AdminAuditActions.SupportTicketStatusChanged,
            TargetType = nameof(SupportTicket),
            TargetId = ticket.Id.ToString(CultureInfo.InvariantCulture),
            CreatedAtUtc = ticket.UpdatedAtUtc
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReplyAsync(
        long ticketId,
        AdminSupportReply reply,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reply);
        var message = reply.Message.Trim();
        if (message.Length is 0 or > 8000)
            throw new InvalidDataException("Support reply must contain between 1 and 8000 characters.");
        if (reply.Status is not null && !Enum.IsDefined(reply.Status.Value))
            throw new InvalidDataException("Invalid support ticket status.");

        var adminUserId = await access.GetRequiredAdminUserIdAsync();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var ticket = await db.SupportTickets.SingleOrDefaultAsync(
            x => x.Id == ticketId,
            cancellationToken);
        if (ticket is null)
            return false;

        var inboxMessage = new InboxMessage
        {
            UserId = ticket.UserId,
            SupportTicketId = ticket.Id,
            Type = InboxMessageType.SupportReply,
            Title = "Ответ от поддержки",
            Preview = CreatePreview(message),
            Body = message,
            CreatedAtUtc = now
        };
        db.InboxMessages.Add(inboxMessage);
        db.Set<SupportMessage>().Add(new SupportMessage
        {
            SupportTicketId = ticket.Id,
            SenderType = SupportMessageSenderType.Support,
            Message = message,
            CreatedAtUtc = now
        });
        ticket.Status = reply.Status ?? SupportTicketStatus.WaitingForUser;
        ticket.UpdatedAtUtc = now;
        db.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminUserId = adminUserId,
            Action = AdminAuditActions.SupportReplySent,
            TargetType = nameof(SupportTicket),
            TargetId = ticket.Id.ToString(CultureInfo.InvariantCulture),
            CreatedAtUtc = now
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        try
        {
            await pushNotifications.NotifyInboxMessageAsync(
                inboxMessage.UserId,
                PushNotificationType.SupportReply,
                inboxMessage.Id,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not send support reply notification for inbox message {InboxMessageId}.",
                inboxMessage.Id);
        }
        return true;
    }

    public async Task<PhotoDownload?> OpenScreenshotAsync(
        long ticketId,
        CancellationToken cancellationToken = default)
    {
        await access.GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var screenshotPath = await db.SupportTickets.AsNoTracking()
            .Where(x => x.Id == ticketId)
            .Select(x => x.ScreenshotPath)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(screenshotPath))
            return null;
        return await screenshots.OpenAsync(screenshotPath, cancellationToken);
    }

    private static string CreatePreview(string message) =>
        message.Length <= 180 ? message : $"{message[..177]}…";

    private sealed record TicketListRow(
        long Id,
        string TicketNumber,
        string UserId,
        string? DisplayName,
        string Email,
        string Message,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        SupportTicketStatus Status,
        bool HasScreenshot);
}
