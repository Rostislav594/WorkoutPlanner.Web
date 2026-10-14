using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Admin;
using WorkoutPlanner.Web.Services.Auth;
using WorkoutPlanner.Web.Services.Push;
using WorkoutPlanner.Web.Tests.Infrastructure;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class AdminPublicationManagementTests
{
    [Fact]
    public async Task Publication_CanBeEditedAndDeleted_AndReportsReadsOfItsAudience()
    {
        await using var app = await TestApplication.CreateAsync();
        var admin = await CreateAdminAsync(app);
        var reader = await app.CreateUserAsync("pub-reader");
        var latecomer = await app.CreateUserAsync("pub-latecomer");
        var publishedAt = DateTime.UtcNow.AddDays(-1);

        await using (var seed = app.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.UserActivities.Add(new UserActivity { UserId = reader.Id, RegisteredAtUtc = publishedAt.AddDays(-10) });
            // Зарегистрировался после публикации — её не видит и в аудиторию не входит.
            db.UserActivities.Add(new UserActivity { UserId = latecomer.Id, RegisteredAtUtc = publishedAt.AddHours(1) });
            await db.SaveChangesAsync();
        }

        long id;
        await using (var scope = app.CreateScope())
        {
            var publications = scope.ServiceProvider.GetRequiredService<IAdminPublicationService>();
            id = (await publications.CreateAsync(new AdminPublicationDraft(
                InboxMessageType.News, "Старый заголовок", "Текст", publishedAt))).Id;
        }

        await using (var seed = app.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.InboxPublicationReads.Add(new InboxPublicationRead { PublicationId = id, UserId = reader.Id, ReadAtUtc = DateTime.UtcNow });
            // Push уже разослан — флаг после этого правкой не меняется.
            (await db.InboxPublications.SingleAsync(x => x.Id == id)).PushSentAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        await using (var scope = app.CreateScope())
        {
            var publications = scope.ServiceProvider.GetRequiredService<IAdminPublicationService>();
            var listed = Assert.Single(await publications.GetRecentAsync());
            Assert.Equal(1, listed.ReadCount);
            Assert.Equal(2, listed.AudienceCount); // админ без даты регистрации и читатель
            Assert.NotNull(listed.PushSentAtUtc);

            var updated = await publications.UpdateAsync(id, new AdminPublicationDraft(
                InboxMessageType.Update, "Новый заголовок", "Новый текст", publishedAt));
            Assert.NotNull(updated);
            Assert.Equal("Новый заголовок", updated.Title);
            Assert.Equal(InboxMessageType.Update, updated.Type);
            Assert.True(updated.SendPushNotification);

            Assert.Null(await publications.UpdateAsync(id + 100, new AdminPublicationDraft(
                InboxMessageType.News, "Нет", "Нет", publishedAt)));

            Assert.True(await publications.DeleteAsync(id));
            Assert.False(await publications.DeleteAsync(id));
        }

        await using var assertScope = app.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        Assert.False(await assertDb.InboxPublications.AnyAsync());
        Assert.False(await assertDb.InboxPublicationReads.AnyAsync());
        Assert.Equal(
            [AdminAuditActions.PublicationCreated, AdminAuditActions.PublicationUpdated, AdminAuditActions.PublicationDeleted],
            await assertDb.AdminAuditLogs.OrderBy(x => x.Id).Select(x => x.Action).ToListAsync());
        Assert.All(await assertDb.AdminAuditLogs.ToListAsync(), log => Assert.Equal(admin.Id, log.AdminUserId));
    }

    [Fact]
    public async Task PushDispatcher_SendsDuePublicationOnceToItsAudienceWithDevices()
    {
        await using var app = await TestApplication.CreateAsync();
        var reader = await app.CreateUserAsync("push-reader");
        var latecomer = await app.CreateUserAsync("push-latecomer");
        var noDevice = await app.CreateUserAsync("push-no-device");
        var now = DateTime.UtcNow;

        long dueId;
        await using (var seed = app.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.UserActivities.Add(new UserActivity { UserId = reader.Id, RegisteredAtUtc = now.AddDays(-10) });
            db.UserActivities.Add(new UserActivity { UserId = latecomer.Id, RegisteredAtUtc = now.AddMinutes(-1) });
            db.UserActivities.Add(new UserActivity { UserId = noDevice.Id, RegisteredAtUtc = now.AddDays(-10) });
            db.PushDeviceRegistrations.Add(Device(reader.Id));
            db.PushDeviceRegistrations.Add(Device(latecomer.Id));

            var due = Publication("Пора", now.AddMinutes(-5), sendPush: true);
            db.InboxPublications.Add(due);
            db.InboxPublications.Add(Publication("Позже", now.AddDays(1), sendPush: true));
            db.InboxPublications.Add(Publication("Без push", now.AddMinutes(-5), sendPush: false));
            await db.SaveChangesAsync();
            dueId = due.Id;
        }

        var dispatcher = new PublicationPushDispatcher(
            app.Services.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<PublicationPushDispatcher>.Instance);
        var pushes = app.Services.GetRequiredService<TestApplication.RecordingRemotePushProvider>();

        await dispatcher.DispatchDueAsync(CancellationToken.None);
        await dispatcher.DispatchDueAsync(CancellationToken.None);

        var push = Assert.Single(pushes.Messages);
        Assert.Equal(reader.Id, push.UserId);
        Assert.Equal(PushNotificationType.Publication, push.Type);
        Assert.Equal(dueId, push.InboxMessageId);
        Assert.Equal("Пора", push.Title);
        Assert.Equal("publication", push.Data[PushNotificationPayloadKeys.Type]);
        Assert.Equal(dueId.ToString(), push.Data[PushNotificationPayloadKeys.InboxMessageId]);

        await using var assertScope = app.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        var sent = await assertDb.InboxPublications.AsNoTracking().ToDictionaryAsync(x => x.Title, x => x.PushSentAtUtc);
        Assert.NotNull(sent["Пора"]);
        Assert.Null(sent["Позже"]);
        Assert.Null(sent["Без push"]);
    }

    [Fact]
    public async Task NewPublication_AlwaysSendsPush_AndAudienceCountsEligibleDevices()
    {
        await using var app = await TestApplication.CreateAsync();
        await CreateAdminAsync(app);
        var reader = await app.CreateUserAsync("push-reader");
        var latecomer = await app.CreateUserAsync("push-latecomer");
        var now = DateTime.UtcNow;
        long oldId;
        await using (var seed = app.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.UserActivities.Add(new UserActivity { UserId = reader.Id, RegisteredAtUtc = now.AddDays(-10) });
            db.UserActivities.Add(new UserActivity { UserId = latecomer.Id, RegisteredAtUtc = now.AddMinutes(-1) });
            db.PushDeviceRegistrations.Add(Device(reader.Id));
            db.PushDeviceRegistrations.Add(Device(latecomer.Id));
            // Старая публикация вышла без push — правка не разошлёт её задним числом.
            var old = Publication("Старая", now.AddDays(-1), sendPush: false);
            db.InboxPublications.Add(old);
            await db.SaveChangesAsync();
            oldId = old.Id;
        }

        await using var scope = app.CreateScope();
        var publications = scope.ServiceProvider.GetRequiredService<IAdminPublicationService>();
        // Публикацию пятиминутной давности видит только читатель; новая видна обоим.
        Assert.Equal(1, await publications.CountPushDevicesAsync(now.AddMinutes(-5)));
        Assert.Equal(2, await publications.CountPushDevicesAsync(now.AddMinutes(1)));

        var created = await publications.CreateAsync(new AdminPublicationDraft(
            InboxMessageType.News, "Новая", "Текст", now.AddDays(1)));
        Assert.True(created.SendPushNotification);

        var edited = await publications.UpdateAsync(oldId, new AdminPublicationDraft(
            InboxMessageType.News, "Старая, поправленная", "Текст", now.AddDays(-1)));
        Assert.False(edited!.SendPushNotification);

        // Перенесли старую публикацию в будущее — при выходе push уйдёт.
        var rescheduled = await publications.UpdateAsync(oldId, new AdminPublicationDraft(
            InboxMessageType.News, "Старая, перенесённая", "Текст", now.AddDays(2)));
        Assert.True(rescheduled!.SendPushNotification);
    }

    private static InboxPublication Publication(string title, DateTime publishedAtUtc, bool sendPush) => new()
    {
        Type = InboxMessageType.News,
        Title = title,
        Preview = $"{title}: текст",
        Body = $"{title}: текст",
        PublishedAtUtc = publishedAtUtc,
        CreatedAtUtc = DateTime.UtcNow,
        SendPushNotification = sendPush
    };

    private static PushDeviceRegistration Device(string userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        InstallationId = $"install-{userId}",
        Platform = "android",
        PushToken = $"token-{userId}",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
        LastSeenAtUtc = DateTime.UtcNow,
        IsActive = true
    };

    private static async Task<IdentityUser> CreateAdminAsync(TestApplication app)
    {
        var admin = await app.CreateUserAsync("pub-admin");
        await using var scope = app.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(ApplicationRoles.Admin))).Succeeded);
        var tracked = await users.FindByIdAsync(admin.Id);
        Assert.NotNull(tracked);
        Assert.True((await users.AddToRoleAsync(tracked, ApplicationRoles.Admin)).Succeeded);
        app.AuthenticationStateProvider.SetUser(admin.Id, ApplicationRoles.Admin);
        return admin;
    }
}
