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

    [Fact]
    public async Task AddWearOsDevicePersistence_UpgradesExistingSetsAndCreatesWatchGraph()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<WorkoutDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new WorkoutDbContext(options);
        await db.Database.MigrateAsync("20260828132217_AddPushDeviceRegistrations");

        db.Users.Add(CreateIdentityUser("user-a"));
        var plan = new TrainingPlan
        {
            UserId = "user-a",
            WorkoutName = "Existing plan",
            Date = DateTime.Today,
            Exercises =
            [
                new Exercise
                {
                    UserId = "user-a",
                    Name = "Existing exercise",
                    WorkoutName = "Existing plan",
                    Sets = []
                }
            ]
        };
        db.TrainingPlans.Add(plan);
        await db.SaveChangesAsync();
        var exerciseId = plan.Exercises[0].Id;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ExerciseTemplateSets
                (ExerciseId, SetNumber, Weight, Repetitions, Completed, IsWarmup)
            VALUES
                ({exerciseId}, 1, 42.5, 8, 0, 0)
            """);
        var setId = await db.ExerciseTemplateSets
            .IgnoreQueryFilters()
            .Where(x => x.ExerciseId == exerciseId)
            .Select(x => x.Id)
            .SingleAsync();

        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();

        var migratedSet = await db.ExerciseTemplateSets.SingleAsync(x => x.Id == setId);
        Assert.Equal(0, migratedSet.Version);
        Assert.Equal(42.5, migratedSet.Weight);

        var now = DateTime.UtcNow;
        var device = new WatchDevice
        {
            Id = Guid.NewGuid(),
            UserId = "user-a",
            DeviceId = "migration-test-watch",
            DisplayName = "Migration test watch",
            Platform = "WearOS",
            CreatedAtUtc = now,
            RefreshTokenHash = "refresh-token-hash",
            RefreshTokenExpiresAtUtc = now.AddDays(30)
        };
        db.WatchDevices.Add(device);
        db.WatchPairingCodes.Add(new WatchPairingCode
        {
            Id = Guid.NewGuid(),
            UserId = "user-a",
            CodeHash = "pairing-code-hash",
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(10)
        });
        db.WatchSyncOperations.Add(new WatchSyncOperation
        {
            OperationId = Guid.NewGuid(),
            WatchDeviceId = device.Id,
            OperationType = "CompleteSet",
            EntityId = setId,
            ReceivedAtUtc = now,
            ExpiresAtUtc = now.Add(WatchSyncOperation.RetentionPeriod),
            ResultJson = "{}"
        });
        await db.SaveChangesAsync();

        Assert.True(await db.WatchDevices.AnyAsync(x => x.Id == device.Id));
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
