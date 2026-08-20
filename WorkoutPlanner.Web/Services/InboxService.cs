using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public sealed class InboxService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    CurrentUserService currentUser,
    TimeProvider timeProvider) : IInboxService
{
    public async Task PublishToUserAsync(
        InboxMessagePublication publication,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publication);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.InboxMessages.Add(new InboxMessage
        {
            UserId = publication.UserId,
            SupportTicketId = publication.SupportTicketId,
            Type = publication.Type,
            Title = publication.Title.Trim(),
            Preview = publication.Preview?.Trim(),
            Body = publication.Body.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<InboxPage> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var messages = await db.InboxMessages
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new InboxMessageItem(
                x.Id,
                x.Type,
                x.Title,
                x.Preview,
                x.Body,
                x.CreatedAtUtc,
                x.ReadAtUtc,
                x.SupportTicket == null ? null : x.SupportTicket.TicketNumber))
            .ToListAsync(cancellationToken);
        return new InboxPage(messages, messages.Count(x => x.ReadAtUtc is null));
    }

    public async Task<int> GetUnreadCountAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.InboxMessages.CountAsync(
            x => x.UserId == userId && x.ReadAtUtc == null,
            cancellationToken);
    }

    public async Task<bool> MarkReadAsync(
        long messageId,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var message = await db.InboxMessages.SingleOrDefaultAsync(
            x => x.Id == messageId && x.UserId == userId,
            cancellationToken);
        if (message is null)
            return false;
        if (message.ReadAtUtc is null)
        {
            message.ReadAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await db.InboxMessages
            .Where(x => x.UserId == userId && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.ReadAtUtc, now),
                cancellationToken);
    }

    public async Task<bool> CreateSupportReplyFromTelegramAsync(
        TelegramSupportReply reply,
        CancellationToken cancellationToken = default)
    {
        var text = reply.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.InboxMessages.AnyAsync(
                x => x.TelegramMessageId == reply.TelegramMessageId,
                cancellationToken))
            return true;

        var ticket = await db.SupportTickets.SingleOrDefaultAsync(
            x => x.TelegramMessageId == reply.ReplyToTelegramMessageId,
            cancellationToken);
        if (ticket is null)
            return false;

        db.InboxMessages.Add(new InboxMessage
        {
            UserId = ticket.UserId,
            SupportTicketId = ticket.Id,
            Type = InboxMessageType.SupportReply,
            Title = "Ответ от поддержки",
            Preview = CreatePreview(text),
            Body = text,
            CreatedAtUtc = reply.CreatedAtUtc,
            TelegramMessageId = reply.TelegramMessageId
        });
        ticket.Status = SupportTicketStatus.WaitingForUser;
        ticket.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string CreatePreview(string text) =>
        text.Length <= 400 ? text : $"{text[..397]}…";
}
