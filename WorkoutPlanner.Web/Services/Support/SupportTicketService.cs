using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services.Support;

public sealed class SupportTicketService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    CurrentUserService currentUser,
    ISupportScreenshotStorage screenshots,
    ISupportNotificationService notifications,
    TimeProvider timeProvider,
    ILogger<SupportTicketService> logger) : ISupportTicketService
{
    public const int MinMessageLength = 10;
    public const int MaxMessageLength = 2000;

    public async Task<SupportTicketCreationResult> CreateAsync(
        SupportTicketSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var message = submission.Message.Trim();
        if (message.Length is < MinMessageLength or > MaxMessageLength)
        {
            if (submission.Screenshot is not null)
                await submission.Screenshot.Content.DisposeAsync();
            return SupportTicketCreationResult.Invalid(
                SupportTicketCreationFailure.InvalidMessage);
        }

        var userId = await currentUser.GetRequiredUserIdAsync();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        string? screenshotPath = null;
        if (submission.Screenshot is not null)
        {
            try
            {
                screenshotPath = await screenshots.SaveAsync(
                    submission.Screenshot,
                    cancellationToken);
            }
            catch (InvalidDataException)
            {
                return SupportTicketCreationResult.Invalid(
                    SupportTicketCreationFailure.InvalidScreenshot);
            }
        }

        SupportTicket ticket;
        string email;
        string displayName;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(
                cancellationToken);
            email = await db.Users
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => x.Email ?? x.UserName ?? string.Empty)
                .SingleAsync(cancellationToken);
            var profile = await db.UserProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { x.FirstName, x.LastName })
                .SingleOrDefaultAsync(cancellationToken);
            displayName = profile is null
                ? string.Empty
                : $"{profile.FirstName} {profile.LastName}".Trim();

            ticket = new SupportTicket
            {
                UserId = userId,
                Message = message,
                ScreenshotPath = screenshotPath,
                AppVersion = NormalizeContext(submission.AppVersion, 40),
                Platform = NormalizeContext(submission.Platform, 40),
                OsVersion = NormalizeContext(submission.OsVersion, 80),
                DeviceModel = NormalizeContext(submission.DeviceModel, 120),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await using var transaction = await db.Database.BeginTransactionAsync(
                cancellationToken);
            db.SupportTickets.Add(ticket);
            await db.SaveChangesAsync(cancellationToken);
            ticket.TicketNumber = $"GP-{now:yyyyMMdd}-{ticket.Id:D6}";
            db.SupportMessages.Add(new SupportMessage
            {
                SupportTicketId = ticket.Id,
                SenderType = SupportMessageSenderType.User,
                Message = ticket.Message,
                CreatedAtUtc = ticket.CreatedAtUtc
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            screenshots.TryDelete(screenshotPath);
            throw;
        }

        logger.LogInformation(
            "Created support ticket {TicketId} ({TicketNumber}).",
            ticket.Id,
            ticket.TicketNumber);

        SupportNotificationResult delivery;
        try
        {
            delivery = await notifications.NotifyTicketCreatedAsync(
                new SupportTicketNotification(
                    ticket.Id,
                    ticket.TicketNumber!,
                    userId,
                    email,
                    displayName,
                    ticket.Message,
                    ticket.ScreenshotPath,
                    ticket.AppVersion,
                    ticket.Platform,
                    ticket.OsVersion,
                    ticket.DeviceModel,
                    ticket.CreatedAtUtc),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Support notification failed unexpectedly for ticket {TicketNumber}.",
                ticket.TicketNumber);
            delivery = SupportNotificationResult.Failed;
        }

        await UpdateDeliveryStatusAsync(ticket.Id, delivery);
        return SupportTicketCreationResult.Success(
            ticket.TicketNumber!,
            ticket.CreatedAtUtc);
    }

    private async Task UpdateDeliveryStatusAsync(
        long ticketId,
        SupportNotificationResult delivery)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var ticket = await db.SupportTickets.FindAsync(ticketId);
            if (ticket is null)
                return;

            ticket.TelegramDeliveryStatus = delivery.Succeeded
                ? SupportDeliveryStatus.Sent
                : delivery.Disabled
                    ? SupportDeliveryStatus.Disabled
                    : SupportDeliveryStatus.Failed;
            ticket.TelegramMessageId = delivery.TelegramMessageId;
            ticket.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not update delivery status for support ticket {TicketId}.",
                ticketId);
        }
    }

    private static string? NormalizeContext(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
