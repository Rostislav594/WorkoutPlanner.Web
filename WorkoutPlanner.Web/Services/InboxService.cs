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
        var personalMessages = await db.InboxMessages
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.DeletedAtUtc == null)
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
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var publications = await db.InboxPublications.AsNoTracking()
            .Where(x => x.PublishedAtUtc <= now &&
                        !x.Reads.Any(read => read.UserId == userId && read.DeletedAtUtc != null))
            .Select(x => new InboxMessageItem(
                x.Id, x.Type, x.Title, x.Preview, x.Body, x.PublishedAtUtc,
                x.Reads.Where(read => read.UserId == userId && read.DeletedAtUtc == null)
                    .Select(read => (DateTime?)read.ReadAtUtc).SingleOrDefault(),
                null, true, x.ImagePath))
            .ToListAsync(cancellationToken);
        var messages = personalMessages.Concat(publications)
            .OrderByDescending(x => x.CreatedAtUtc).ToList();
        return new InboxPage(messages, messages.Count(x => x.ReadAtUtc is null));
    }

    public async Task<int> GetUnreadCountAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var personal = await db.InboxMessages.CountAsync(
            x => x.UserId == userId && x.ReadAtUtc == null && x.DeletedAtUtc == null,
            cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var publications = await db.InboxPublications.CountAsync(
            x => x.PublishedAtUtc <= now && !x.Reads.Any(read => read.UserId == userId),
            cancellationToken);
        return personal + publications;
    }

    public async Task<bool> MarkReadAsync(
        long messageId,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var message = await db.InboxMessages.SingleOrDefaultAsync(
            x => x.Id == messageId && x.UserId == userId && x.DeletedAtUtc == null,
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

    public async Task<bool> MarkPublicationReadAsync(
        long publicationId,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!await db.InboxPublications.AnyAsync(
                x => x.Id == publicationId && x.PublishedAtUtc <= now,
                cancellationToken))
            return false;
        var readState = await db.InboxPublicationReads.SingleOrDefaultAsync(
                x => x.PublicationId == publicationId && x.UserId == userId,
                cancellationToken);
        if (readState is null)
        {
            db.InboxPublicationReads.Add(new InboxPublicationRead
            {
                PublicationId = publicationId,
                UserId = userId,
                ReadAtUtc = now
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task<bool> DeleteMessageAsync(long messageId, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var message = await db.InboxMessages.SingleOrDefaultAsync(
            x => x.Id == messageId && x.UserId == userId && x.DeletedAtUtc == null,
            cancellationToken);
        if (message is null) return false;
        message.DeletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeletePublicationAsync(long publicationId, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!await db.InboxPublications.AnyAsync(x => x.Id == publicationId && x.PublishedAtUtc <= now, cancellationToken)) return false;
        var state = await db.InboxPublicationReads.SingleOrDefaultAsync(
            x => x.PublicationId == publicationId && x.UserId == userId, cancellationToken);
        if (state is null)
        {
            db.InboxPublicationReads.Add(new InboxPublicationRead
            {
                PublicationId = publicationId, UserId = userId, DeletedAtUtc = now
            });
        }
        else if (state.DeletedAtUtc is null)
        {
            state.DeletedAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await db.InboxMessages.Where(x => x.UserId == userId && x.DeletedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.DeletedAtUtc, now), cancellationToken);
        var publicationIds = await db.InboxPublications
            .Where(x => x.PublishedAtUtc <= now)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var states = await db.InboxPublicationReads.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var stateIds = states.Select(x => x.PublicationId).ToHashSet();
        foreach (var state in states.Where(x => publicationIds.Contains(x.PublicationId)))
            state.DeletedAtUtc = now;
        db.InboxPublicationReads.AddRange(publicationIds.Where(id => !stateIds.Contains(id)).Select(id => new InboxPublicationRead
        {
            PublicationId = id, UserId = userId, DeletedAtUtc = now
        }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await db.InboxMessages
            .Where(x => x.UserId == userId && x.ReadAtUtc == null && x.DeletedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.ReadAtUtc, now),
                cancellationToken);
        var publicationIds = await db.InboxPublications
            .Where(x => x.PublishedAtUtc <= now && !x.Reads.Any(read => read.UserId == userId))
            .Select(x => x.Id).ToListAsync(cancellationToken);
        if (publicationIds.Count > 0)
        {
            db.InboxPublicationReads.AddRange(publicationIds.Select(id => new InboxPublicationRead
            {
                PublicationId = id,
                UserId = userId,
                ReadAtUtc = now
            }));
            await db.SaveChangesAsync(cancellationToken);
        }
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
        db.SupportMessages.Add(new SupportMessage
        {
            SupportTicketId = ticket.Id,
            SenderType = SupportMessageSenderType.Support,
            Message = text,
            CreatedAtUtc = reply.CreatedAtUtc
        });
        ticket.Status = SupportTicketStatus.WaitingForUser;
        ticket.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string CreatePreview(string text) =>
        text.Length <= 400 ? text : $"{text[..397]}…";
}
