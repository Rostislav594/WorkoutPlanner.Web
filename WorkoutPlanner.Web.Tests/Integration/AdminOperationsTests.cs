using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Admin;
using WorkoutPlanner.Web.Services.Auth;
using WorkoutPlanner.Web.Tests.Infrastructure;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class AdminOperationsTests
{
    [Fact]
    public async Task SupportReply_CreatesHistoryInboxAuditAndStatusAtomically()
    {
        await using var app = await TestApplication.CreateAsync();
        var admin = await app.CreateUserAsync("admin-user");
        var user = await app.CreateUserAsync("support-user");

        await using (var roleScope = app.CreateScope())
        {
            var roles = roleScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = roleScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(ApplicationRoles.Admin))).Succeeded);
            var trackedAdmin = await users.FindByIdAsync(admin.Id);
            Assert.NotNull(trackedAdmin);
            Assert.True((await users.AddToRoleAsync(trackedAdmin, ApplicationRoles.Admin)).Succeeded);
        }

        long ticketId;
        await using (var seedScope = app.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            var ticket = new SupportTicket
            {
                UserId = user.Id,
                TicketNumber = "GP-TEST-000001",
                Message = "Исходное обращение пользователя",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                TelegramDeliveryStatus = SupportDeliveryStatus.Disabled
            };
            db.SupportTickets.Add(ticket);
            db.SupportMessages.Add(new SupportMessage
            {
                SupportTicket = ticket,
                SenderType = SupportMessageSenderType.User,
                Message = ticket.Message,
                CreatedAtUtc = ticket.CreatedAtUtc
            });
            await db.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        app.AuthenticationStateProvider.SetUser(admin.Id);
        await using (var operationScope = app.CreateScope())
        {
            var support = operationScope.ServiceProvider.GetRequiredService<IAdminSupportService>();
            Assert.True(await support.ReplyAsync(
                ticketId,
                new AdminSupportReply("Ответ администратора", SupportTicketStatus.WaitingForUser)));
        }

        await using var assertScope = app.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        var ticketAfter = await assertDb.SupportTickets.AsNoTracking().SingleAsync(x => x.Id == ticketId);
        Assert.Equal(SupportTicketStatus.WaitingForUser, ticketAfter.Status);
        Assert.Equal(2, await assertDb.SupportMessages.CountAsync(x => x.SupportTicketId == ticketId));
        var inbox = await assertDb.InboxMessages.AsNoTracking().SingleAsync(x => x.SupportTicketId == ticketId);
        Assert.Equal(user.Id, inbox.UserId);
        Assert.Equal("Ответ администратора", inbox.Body);
        var audit = await assertDb.AdminAuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(AdminAuditActions.SupportReplySent, audit.Action);
        Assert.Equal(admin.Id, audit.AdminUserId);
        Assert.Equal(ticketId.ToString(), audit.TargetId);
    }

    [Fact]
    public async Task SupportReply_SendsPushOnlyAfterInboxMessageIsPersisted()
    {
        var push = new RecordingPushNotificationService();
        await using var app = await TestApplication.CreateAsync(push);
        var admin = await app.CreateUserAsync("push-admin");
        var user = await app.CreateUserAsync("push-user");
        await GrantAdminRoleAsync(app, admin);
        var ticketId = await SeedTicketAsync(app, user.Id, "GP-PUSH-000001");

        push.OnNotify = async (_, _, inboxMessageId) =>
        {
            await using var scope = app.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            Assert.True(await db.InboxMessages.AsNoTracking().AnyAsync(x =>
                x.Id == inboxMessageId &&
                x.UserId == user.Id &&
                x.Body == "PRIVATE_SENTINEL_reply_saved_before_push"));
        };

        app.AuthenticationStateProvider.SetUser(admin.Id);
        await using (var scope = app.CreateScope())
        {
            var support = scope.ServiceProvider.GetRequiredService<IAdminSupportService>();
            Assert.True(await support.ReplyAsync(
                ticketId,
                new AdminSupportReply("PRIVATE_SENTINEL_reply_saved_before_push")));
        }

        var notification = Assert.Single(push.Notifications);
        Assert.Equal(user.Id, notification.UserId);
        Assert.Equal(PushNotificationType.SupportReply, notification.Type);
        Assert.True(notification.InboxMessageId > 0);
    }

    [Fact]
    public async Task SupportReply_WhenPushFails_KeepsCommittedInboxMessage()
    {
        var push = new RecordingPushNotificationService
        {
            OnNotify = (_, _, _) => throw new HttpRequestException("Push unavailable.")
        };
        await using var app = await TestApplication.CreateAsync(push);
        var admin = await app.CreateUserAsync("failure-admin");
        var user = await app.CreateUserAsync("failure-user");
        await GrantAdminRoleAsync(app, admin);
        var ticketId = await SeedTicketAsync(app, user.Id, "GP-PUSH-000002");

        app.AuthenticationStateProvider.SetUser(admin.Id);
        await using (var scope = app.CreateScope())
        {
            var support = scope.ServiceProvider.GetRequiredService<IAdminSupportService>();
            Assert.True(await support.ReplyAsync(
                ticketId,
                new AdminSupportReply("Ответ сохранён независимо от push")));
        }

        await using var assertScope = app.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        var inbox = await assertDb.InboxMessages.AsNoTracking()
            .SingleAsync(x => x.SupportTicketId == ticketId);
        Assert.Equal(user.Id, inbox.UserId);
        Assert.Equal("Ответ сохранён независимо от push", inbox.Body);
        Assert.Single(push.Notifications);
    }

    private static async Task GrantAdminRoleAsync(
        TestApplication app,
        IdentityUser admin)
    {
        await using var scope = app.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(ApplicationRoles.Admin))).Succeeded);
        var trackedAdmin = await users.FindByIdAsync(admin.Id);
        Assert.NotNull(trackedAdmin);
        Assert.True((await users.AddToRoleAsync(trackedAdmin, ApplicationRoles.Admin)).Succeeded);
    }

    private static async Task<long> SeedTicketAsync(
        TestApplication app,
        string userId,
        string ticketNumber)
    {
        await using var scope = app.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        var ticket = new SupportTicket
        {
            UserId = userId,
            TicketNumber = ticketNumber,
            Message = "Исходное обращение",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            TelegramDeliveryStatus = SupportDeliveryStatus.Disabled
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket.Id;
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
