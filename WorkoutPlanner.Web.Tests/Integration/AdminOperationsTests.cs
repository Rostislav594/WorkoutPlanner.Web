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

    [Fact]
    public async Task Attention_CountsOnlyWhatAppearedAfterTheSectionWasViewed()
    {
        await using var app = await TestApplication.CreateAsync();
        var admin = await app.CreateUserAsync("attention-admin");
        var oldUser = await app.CreateUserAsync("attention-old-user");
        var newUser = await app.CreateUserAsync("attention-new-user");
        app.AuthenticationStateProvider.SetUser(admin.Id, ApplicationRoles.Admin);

        // Первое обращение задаёт точку отсчёта: прошлое не считается новым.
        await using (var scope = app.CreateScope())
        {
            var attention = scope.ServiceProvider.GetRequiredService<IAdminAttentionService>();
            Assert.Equal(new AdminAttentionSnapshot(0, 0), await attention.GetAsync());
        }

        var now = DateTime.UtcNow;
        await using (var seedScope = app.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            foreach (var view in await db.AdminSectionViews.ToListAsync())
                view.LastViewedAtUtc = now.AddMinutes(-10);

            db.UserActivities.Add(new UserActivity { UserId = oldUser.Id, RegisteredAtUtc = now.AddMinutes(-30) });
            db.UserActivities.Add(new UserActivity { UserId = newUser.Id, RegisteredAtUtc = now.AddMinutes(-5) });

            var oldTicket = NewTicket(oldUser.Id, "GP-ATT-000001", now.AddMinutes(-30));
            db.SupportTickets.Add(oldTicket);
            // Ответ пользователя после просмотра делает старое обращение непросмотренным.
            db.SupportMessages.Add(new SupportMessage { SupportTicket = oldTicket, SenderType = SupportMessageSenderType.User, Message = "Ещё вопрос", CreatedAtUtc = now.AddMinutes(-4) });

            var answeredTicket = NewTicket(oldUser.Id, "GP-ATT-000002", now.AddMinutes(-30));
            db.SupportTickets.Add(answeredTicket);
            // Ответ поддержки — не повод подсвечивать.
            db.SupportMessages.Add(new SupportMessage { SupportTicket = answeredTicket, SenderType = SupportMessageSenderType.Support, Message = "Ответ", CreatedAtUtc = now.AddMinutes(-4) });

            db.SupportTickets.Add(NewTicket(newUser.Id, "GP-ATT-000003", now.AddMinutes(-3)));
            await db.SaveChangesAsync();
        }

        await using (var scope = app.CreateScope())
        {
            var attention = scope.ServiceProvider.GetRequiredService<IAdminAttentionService>();
            Assert.Equal(new AdminAttentionSnapshot(1, 2), await attention.GetAsync());

            var changed = 0;
            attention.Changed += () => changed++;
            await attention.MarkViewedAsync(AdminSection.Users);

            Assert.Equal(1, changed);
            Assert.Equal(new AdminAttentionSnapshot(0, 2), await attention.GetAsync());

            await attention.MarkViewedAsync(AdminSection.Support);
            Assert.Equal(new AdminAttentionSnapshot(0, 0), await attention.GetAsync());
        }
    }

    [Fact]
    public async Task Attention_RequiresAdminRole()
    {
        await using var app = await TestApplication.CreateAsync();
        var user = await app.CreateUserAsync("attention-plain-user");
        app.AuthenticationStateProvider.SetUser(user.Id);

        await using var scope = app.CreateScope();
        var attention = scope.ServiceProvider.GetRequiredService<IAdminAttentionService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => attention.GetAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => attention.MarkViewedAsync(AdminSection.Support));
    }

    private static SupportTicket NewTicket(string userId, string ticketNumber, DateTime createdAtUtc) => new()
    {
        UserId = userId,
        TicketNumber = ticketNumber,
        Message = "Обращение",
        CreatedAtUtc = createdAtUtc,
        UpdatedAtUtc = createdAtUtc,
        TelegramDeliveryStatus = SupportDeliveryStatus.Disabled
    };

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
