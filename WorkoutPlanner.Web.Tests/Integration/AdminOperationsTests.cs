using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
}
