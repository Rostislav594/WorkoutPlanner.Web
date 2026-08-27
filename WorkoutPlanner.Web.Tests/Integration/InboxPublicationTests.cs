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
