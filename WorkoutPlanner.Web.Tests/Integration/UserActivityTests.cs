using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Activity;
using WorkoutPlanner.Web.Tests.Infrastructure;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class UserActivityTests
{
    [Fact]
    public async Task RecordSeen_UsesDatabaseThrottle_AndPreservesKnownMetadata()
    {
        await using var app = await TestApplication.CreateAsync();
        await app.CreateUserAsync("activity-user");
        await using var scope = app.CreateScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        var clock = new MutableTimeProvider(
            new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero));
        var service = new UserActivityService(
            dbFactory,
            Options.Create(new UserActivityOptions
            {
                WriteThrottle = TimeSpan.FromMinutes(15)
            }),
            clock);

        await service.EnsureRegisteredAsync(
            "activity-user",
            clock.GetUtcNow().UtcDateTime);
        await service.RecordSeenAsync(
            "activity-user",
            new UserActivityMetadata("Android", "1.2.3", "16", "Test Phone"));

        clock.Advance(TimeSpan.FromMinutes(5));
        await service.RecordSeenAsync(
            "activity-user",
            new UserActivityMetadata(null, null, null, null));

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            var activity = await db.UserActivities.AsNoTracking().SingleAsync();
            Assert.Equal(new DateTime(2026, 8, 26, 10, 0, 0), activity.LastSeenAtUtc);
            Assert.Equal("Android", activity.Platform);
            Assert.Equal("1.2.3", activity.AppVersion);
            Assert.Equal("16", activity.OsVersion);
            Assert.Equal("Test Phone", activity.DeviceModel);
        }

        clock.Advance(TimeSpan.FromMinutes(11));
        await service.RecordSeenAsync(
            "activity-user",
            new UserActivityMetadata(null, null, null, null));

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            var activity = await db.UserActivities.AsNoTracking().SingleAsync();
            Assert.Equal(new DateTime(2026, 8, 26, 10, 16, 0), activity.LastSeenAtUtc);
            Assert.Equal("Android", activity.Platform);
            Assert.Equal("1.2.3", activity.AppVersion);
        }
    }

    [Fact]
    public async Task RecordSeen_MarksEachActiveDayOnce_AndWritesTheFirstVisitAfterMidnight()
    {
        await using var app = await TestApplication.CreateAsync();
        await app.CreateUserAsync("activity-day-user");
        await using var scope = app.CreateScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        var clock = new MutableTimeProvider(
            new DateTimeOffset(2026, 8, 26, 23, 55, 0, TimeSpan.Zero));
        var service = new UserActivityService(
            dbFactory,
            Options.Create(new UserActivityOptions
            {
                WriteThrottle = TimeSpan.FromMinutes(15)
            }),
            clock);
        var metadata = new UserActivityMetadata(null, null, null, null);

        await service.RecordSeenAsync("activity-day-user", metadata);
        clock.Advance(TimeSpan.FromMinutes(3));
        await service.RecordSeenAsync("activity-day-user", metadata);
        // Пауза между записями ещё не прошла, но наступил новый день — заход записывается.
        clock.Advance(TimeSpan.FromMinutes(5));
        await service.RecordSeenAsync("activity-day-user", metadata);

        await using var db = await dbFactory.CreateDbContextAsync();
        Assert.Equal(
            [new DateOnly(2026, 8, 26), new DateOnly(2026, 8, 27)],
            await db.UserActivityDays.AsNoTracking()
                .Where(x => x.UserId == "activity-day-user")
                .OrderBy(x => x.Day)
                .Select(x => x.Day)
                .ToListAsync());
        Assert.Equal(
            new DateTime(2026, 8, 27, 0, 3, 0),
            (await db.UserActivities.AsNoTracking().SingleAsync()).LastSeenAtUtc);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        // Дни активности считаются по местному времени; в тестах оно — UTC.
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
