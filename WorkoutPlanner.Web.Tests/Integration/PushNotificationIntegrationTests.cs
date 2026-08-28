using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Tests.Infrastructure;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class PushNotificationIntegrationTests
{
    [Fact]
    public async Task TelegramSupportReply_PersistsBeforePush_AndDuplicateDoesNotPushAgain()
    {
        var push = new RecordingPushNotificationService();
        await using var app = await TestApplication.CreateAsync(push);
        var user = await app.CreateUserAsync("telegram-push-user");
        await using (var scope = app.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.SupportTickets.Add(new SupportTicket
            {
                UserId = user.Id,
                TicketNumber = "GP-TELEGRAM-PUSH-1",
                Message = "Исходное обращение",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                TelegramMessageId = 7001
            });
            await db.SaveChangesAsync();
        }

        push.OnNotify = async (_, _, messageId) =>
        {
            await using var scope = app.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            Assert.True(await db.InboxMessages.AsNoTracking().AnyAsync(x =>
                x.Id == messageId &&
                x.Body == "PRIVATE_SENTINEL_telegram_support_reply"));
        };

        var reply = new TelegramSupportReply(
            7002,
            7001,
            "PRIVATE_SENTINEL_telegram_support_reply",
            DateTime.UtcNow);
        await using (var scope = app.CreateScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
            Assert.True(await inbox.CreateSupportReplyFromTelegramAsync(reply));
            Assert.True(await inbox.CreateSupportReplyFromTelegramAsync(reply));
        }

        var notification = Assert.Single(push.Notifications);
        Assert.Equal(user.Id, notification.UserId);
        Assert.Equal(PushNotificationType.SupportReply, notification.Type);
        Assert.True(notification.InboxMessageId > 0);

        await using var assertScope = app.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        Assert.Equal(1, await assertDb.InboxMessages.CountAsync());
        Assert.Equal(1, await assertDb.SupportMessages.CountAsync());
    }

    private sealed class RecordingPushNotificationService : IPushNotificationService
    {
        public List<Notification> Notifications { get; } = [];

        public Func<string, PushNotificationType, long, Task>? OnNotify { get; set; }

        public async Task NotifyInboxMessageAsync(
            string userId,
            PushNotificationType type,
            long inboxMessageId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Notifications.Add(new Notification(userId, type, inboxMessageId));
            if (OnNotify is not null)
                await OnNotify(userId, type, inboxMessageId);
        }

        public sealed record Notification(
            string UserId,
            PushNotificationType Type,
            long InboxMessageId);
    }
}
