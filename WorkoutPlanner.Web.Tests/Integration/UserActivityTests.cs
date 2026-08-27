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

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
