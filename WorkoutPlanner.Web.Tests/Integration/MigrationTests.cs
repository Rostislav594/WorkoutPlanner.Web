using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class MigrationTests
{
    [Fact]
    public async Task PersonalizeStarterPlans_CreatesPlansPerExistingUser_AndPreservesLegacyRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<WorkoutDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new WorkoutDbContext(options);

        await db.Database.MigrateAsync("20260804111224_AddExercisePhoto");

        db.Users.AddRange(
            CreateIdentityUser("user-a"),
            CreateIdentityUser("user-b"));
        db.TrainingPlans.Add(
            new TrainingPlan
            {
                UserId = null,
                WorkoutName = "Legacy global plan",
                Date = DateTime.Today
            });
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();

        var userAPlans = await db.TrainingPlans
            .Where(x => x.UserId == "user-a")
            .Select(x => x.WorkoutName)
            .OrderBy(x => x)
            .ToListAsync();
        var userBPlans = await db.TrainingPlans
            .Where(x => x.UserId == "user-b")
            .Select(x => x.WorkoutName)
            .OrderBy(x => x)
            .ToListAsync();

        Assert.Equal(4, userAPlans.Count);
        Assert.Equal(userAPlans, userBPlans);
        Assert.Contains("Верх 1", userAPlans);
        Assert.Contains("Низ 2", userAPlans);
        Assert.True(await db.TrainingPlans.AnyAsync(x =>
            x.UserId == null &&
            x.WorkoutName == "Legacy global plan"));

        var mobileSession = new MobileSession
        {
            Id = Guid.NewGuid(),
            UserId = "user-a",
            DeviceName = "Migration test phone",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
        db.MobileSessions.Add(mobileSession);
        await db.SaveChangesAsync();
        Assert.True(await db.MobileSessions.AnyAsync(x =>
            x.Id == mobileSession.Id && x.UserId == "user-a"));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private static IdentityUser CreateIdentityUser(string id)
    {
        return new IdentityUser
        {
            Id = id,
            UserName = $"{id}@example.test",
            NormalizedUserName = $"{id.ToUpperInvariant()}@EXAMPLE.TEST",
            Email = $"{id}@example.test",
            NormalizedEmail = $"{id.ToUpperInvariant()}@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
    }
}
