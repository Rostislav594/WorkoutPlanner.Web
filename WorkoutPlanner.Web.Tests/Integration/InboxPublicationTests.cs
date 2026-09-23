using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Tests.Infrastructure;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class InboxPublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegistrationCutoff_AppliesToListCountAndSingleAndBulkActions(bool deleteAll)
    {
        await using var app = await TestApplication.CreateAsync();
        await app.CreateUserAsync("new-user");
        await app.CreateUserAsync("existing-user");
        var registered = DateTime.UtcNow.AddHours(-1);
        await using var scope = app.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        db.UserActivities.AddRange(
            new UserActivity { UserId = "new-user", RegisteredAtUtc = registered },
            new UserActivity { UserId = "existing-user", RegisteredAtUtc = registered.AddDays(-1) });
        InboxPublication Publication(string title, DateTime published) => new()
        {
            Type = InboxMessageType.News, Title = title, Body = title,
            CreatedAtUtc = registered.AddDays(-2), PublishedAtUtc = published
        };
        var old = Publication("Before registration", registered.AddTicks(-1));
        var boundary = Publication("At registration", registered);
        var recent = Publication("After registration", registered.AddMinutes(1));
        var future = Publication("Scheduled", DateTime.UtcNow.AddDays(1));
        db.InboxPublications.AddRange(old, boundary, recent, future);
        db.InboxMessages.Add(new InboxMessage
        {
            UserId = "new-user", Type = InboxMessageType.SupportReply,
            Title = "Personal", Body = "Personal", CreatedAtUtc = registered.AddDays(-1)
        });
        await db.SaveChangesAsync();
        app.AuthenticationStateProvider.SetUser("new-user");
        var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
        var page = await inbox.GetCurrentAsync();
        Assert.Equal(3, page.Messages.Count);
        Assert.Equal(3, page.UnreadCount);
        Assert.Equal(page.UnreadCount, await inbox.GetUnreadCountAsync());
        Assert.DoesNotContain(page.Messages, x => x.IsPublication && (x.Id == old.Id || x.Id == future.Id));
        Assert.False(await inbox.MarkPublicationReadAsync(old.Id));
        Assert.False(await inbox.DeletePublicationAsync(old.Id));
        Assert.False(await inbox.MarkPublicationReadAsync(future.Id));
        Assert.False(await inbox.DeletePublicationAsync(future.Id));
        Assert.True(await inbox.MarkPublicationReadAsync(boundary.Id));
        Assert.Equal(2, await inbox.GetUnreadCountAsync());
        if (deleteAll) await inbox.DeleteAllAsync();
        else await inbox.MarkAllReadAsync();
        Assert.Equal(0, await inbox.GetUnreadCountAsync());
        var after = await inbox.GetCurrentAsync();
        Assert.Equal(0, after.UnreadCount);
        Assert.Equal(deleteAll ? 0 : 3, after.Messages.Count);
        Assert.False(await db.InboxPublicationReads.AnyAsync(x => x.PublicationId == old.Id || x.PublicationId == future.Id));
        app.AuthenticationStateProvider.SetUser("existing-user");
        Assert.Equal(3, (await inbox.GetCurrentAsync()).Messages.Count);
        Assert.Equal(3, await inbox.GetUnreadCountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyAccountWithoutRegistrationDate_KeepsPublishedNotifications(bool hasActivity)
    {
        await using var app = await TestApplication.CreateAsync();
        await app.CreateUserAsync("legacy");
        await using var scope = app.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        if (hasActivity) db.UserActivities.Add(new UserActivity { UserId = "legacy", RegisteredAtUtc = null });
        db.InboxPublications.Add(new InboxPublication
        {
            Type = InboxMessageType.News, Title = "Old news", Body = "Old news",
            PublishedAtUtc = DateTime.UtcNow.AddYears(-1), CreatedAtUtc = DateTime.UtcNow.AddYears(-1)
        });
        await db.SaveChangesAsync();
        app.AuthenticationStateProvider.SetUser("legacy");
        var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
        Assert.Single((await inbox.GetCurrentAsync()).Messages);
        Assert.Equal(1, await inbox.GetUnreadCountAsync());
    }

    [Fact]
    public async Task GlobalPublication_UsesIndependentReadState_AndMarkAllReadsBothSources()
    {
        await using var app = await TestApplication.CreateAsync();
        await app.CreateUserAsync("first");
        await app.CreateUserAsync("second");
        await using (var scope = app.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.InboxPublications.Add(new InboxPublication
            {
                Type = InboxMessageType.News,
                Title = "Новость",
                Preview = "Текст",
                Body = "Один общий текст публикации",
                PublishedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = "first"
            });
            db.InboxMessages.Add(new InboxMessage
            {
                UserId = "first", Type = InboxMessageType.SupportReply,
                Title = "Поддержка", Body = "Ответ", CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        app.AuthenticationStateProvider.SetUser("first");
        await using (var scope = app.CreateScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
            var page = await inbox.GetCurrentAsync();
            Assert.Equal(2, page.UnreadCount);
            var publication = Assert.Single(page.Messages, x => x.IsPublication);
            Assert.True(await inbox.MarkPublicationReadAsync(publication.Id));
            Assert.Equal(1, await inbox.GetUnreadCountAsync());
            await inbox.MarkAllReadAsync();
            Assert.Equal(0, await inbox.GetUnreadCountAsync());
        }

        app.AuthenticationStateProvider.SetUser("second");
        await using (var scope = app.CreateScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
            Assert.Equal(1, await inbox.GetUnreadCountAsync());
            Assert.Single((await inbox.GetCurrentAsync()).Messages);
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            Assert.Single(await db.InboxPublications.ToListAsync());
            Assert.Single(await db.InboxPublicationReads.ToListAsync());
        }
    }

    [Fact]
    public async Task ScheduledPublication_IsHiddenUntilPublishTime()
    {
        await using var app = await TestApplication.CreateAsync();
        await app.CreateUserAsync("author");
        app.AuthenticationStateProvider.SetUser("author");
        await using var scope = app.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        db.InboxPublications.Add(new InboxPublication
        {
            Type = InboxMessageType.System, Title = "Позже", Body = "Текст",
            PublishedAtUtc = DateTime.UtcNow.AddHours(1), CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "author"
        });
        await db.SaveChangesAsync();
        var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
        Assert.Empty((await inbox.GetCurrentAsync()).Messages);
        Assert.Equal(0, await inbox.GetUnreadCountAsync());
    }

    [Fact]
    public async Task PublicationImageStorage_RejectsSpoofedImage_AndRemovesFile()
    {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IInboxPublicationImageStorage>();
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(
            new PhotoUpload(new MemoryStream("not an image"u8.ToArray()), "image/png", 12)));
        var folder = Path.Combine(app.ContentRootPath, "App_Data", "PublicationImages");
        Assert.True(!Directory.Exists(folder) || Directory.GetFiles(folder).Length == 0);
    }

    [Fact]
    public async Task DeleteMessage_IsScopedToCurrentUser_AndRemovesUnreadCount()
    {
        await using var app = await TestApplication.CreateAsync();
        await app.CreateUserAsync("first");
        await app.CreateUserAsync("second");
        await using (var scope = app.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.InboxMessages.Add(new InboxMessage
            {
                UserId = "first", Type = InboxMessageType.System,
                Title = "Личное", Body = "Текст", CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        app.AuthenticationStateProvider.SetUser("first");
        await using (var scope = app.CreateScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
            var message = Assert.Single((await inbox.GetCurrentAsync()).Messages);
            Assert.True(await inbox.DeleteMessageAsync(message.Id));
            Assert.Empty((await inbox.GetCurrentAsync()).Messages);
            Assert.Equal(0, await inbox.GetUnreadCountAsync());
        }

        app.AuthenticationStateProvider.SetUser("second");
        await using (var scope = app.CreateScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
            Assert.Empty((await inbox.GetCurrentAsync()).Messages);
        }
    }

    [Fact]
    public async Task GetMessage_IsScopedToCurrentUser_AndExcludesDeletedMessages()
    {
        await using var app = await TestApplication.CreateAsync();
        await app.CreateUserAsync("message-owner");
        await app.CreateUserAsync("other-user");
        long messageId;
        await using (var scope = app.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            var message = new InboxMessage
            {
                UserId = "message-owner",
                Type = InboxMessageType.SupportReply,
                Title = "Ответ поддержки",
                Body = "PRIVATE_SENTINEL_owner_only_body",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.InboxMessages.Add(message);
            await db.SaveChangesAsync();
            messageId = message.Id;
        }

        app.AuthenticationStateProvider.SetUser("other-user");
        await using (var scope = app.CreateScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
            Assert.Null(await inbox.GetMessageAsync(messageId));
        }

        app.AuthenticationStateProvider.SetUser("message-owner");
        await using (var scope = app.CreateScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
            var owned = await inbox.GetMessageAsync(messageId);
            Assert.NotNull(owned);
            Assert.Equal("PRIVATE_SENTINEL_owner_only_body", owned.Body);
            Assert.True(await inbox.DeleteMessageAsync(messageId));
            Assert.Null(await inbox.GetMessageAsync(messageId));
        }
    }

    [Fact]
    public async Task DeleteAll_HidesPersonalAndGlobalMessagesOnlyForCurrentUser()
    {
        await using var app = await TestApplication.CreateAsync();
        await app.CreateUserAsync("first");
        await app.CreateUserAsync("second");
        await using (var scope = app.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.InboxPublications.Add(new InboxPublication
            {
                Type = InboxMessageType.News, Title = "Общая новость", Body = "Текст",
                PublishedAtUtc = DateTime.UtcNow.AddMinutes(-1), CreatedAtUtc = DateTime.UtcNow
            });
            db.InboxMessages.Add(new InboxMessage
            {
                UserId = "first", Type = InboxMessageType.System,
                Title = "Личное", Body = "Текст", CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        app.AuthenticationStateProvider.SetUser("first");
        await using (var scope = app.CreateScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
            Assert.Equal(2, (await inbox.GetCurrentAsync()).Messages.Count);
            await inbox.MarkAllReadAsync();
            Assert.Equal(0, await inbox.GetUnreadCountAsync());
            await inbox.DeleteAllAsync();
            Assert.Empty((await inbox.GetCurrentAsync()).Messages);
            Assert.Equal(0, await inbox.GetUnreadCountAsync());
        }

        app.AuthenticationStateProvider.SetUser("second");
        await using (var scope = app.CreateScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxService>();
            Assert.Single((await inbox.GetCurrentAsync()).Messages);
            Assert.Equal(1, await inbox.GetUnreadCountAsync());
        }
    }
}
