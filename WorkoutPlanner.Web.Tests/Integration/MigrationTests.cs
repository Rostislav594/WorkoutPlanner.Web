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
        await db.SaveChangesAsync();

        // Старая строка вставляется чистым SQL с перечислением колонок: модель
        // EF описывает сегодняшнюю схему, а база здесь намеренно откачена назад.
        // Через DbSet вставка ломалась бы при каждой новой колонке плана.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO TrainingPlans (UserId, WorkoutName, Date)
            VALUES (NULL, 'Legacy global plan', {DateTime.Today})
            """);

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
        await db.SaveChangesAsync();

        // План — тоже старой схемы, поэтому идёт чистым SQL (см. пояснение выше).
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO TrainingPlans (UserId, WorkoutName, Date)
            VALUES ('user-a', 'Existing plan', {DateTime.Today})
            """);
        var planId = await db.TrainingPlans
            .IgnoreQueryFilters()
            .Where(x => x.WorkoutName == "Existing plan")
            .Select(x => x.Id)
            .SingleAsync();

        // Упражнение старой схемы тоже вставляется чистым SQL: у модели уже есть
        // колонки, которых в этой версии базы ещё нет.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Exercises
                (UserId, Name, WorkoutName, SetsCount, Set1Completed, Set2Completed,
                 Set3Completed, Status, TrainingPlanId)
            VALUES
                ('user-a', 'Existing exercise', 'Existing plan', 3, 0, 0, 0, 0, {planId})
            """);
        var exerciseId = await db.Exercises
            .IgnoreQueryFilters()
            .Where(x => x.TrainingPlanId == planId)
            .Select(x => x.Id)
            .SingleAsync();
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

    [Fact]
    public async Task AddRestTimersToTemplates_CopiesProfileTimersIntoExistingTemplates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<WorkoutDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new WorkoutDbContext(options);
        await db.Database.MigrateAsync("20260928055254_AddMobileIdempotencyRecords");

        db.Users.AddRange(
            CreateIdentityUser("user-a"),
            CreateIdentityUser("user-b"));
        await db.SaveChangesAsync();

        // У первого свои таймеры в профиле, у второго профиля нет вовсе.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO UserProfiles (UserId, FirstName, LastName, Gender, CreatedAt, UpdatedAt, RestBetweenSetsSeconds, RestBetweenExercisesSeconds, PreferredLanguage)
            VALUES ('user-a', 'A', 'A', '', '2026-01-01', '2026-01-01', 75, 150, 'ru');
            INSERT INTO TrainingPlans (UserId, WorkoutName, Date, IsFreeDraft)
            VALUES ('user-a', 'Plan A', '2026-01-01', 0), ('user-b', 'Plan B', '2026-01-01', 0);
            INSERT INTO Exercises (UserId, Name, WorkoutName, SetsCount, Set1Completed, Set2Completed, Set3Completed, Status, TrainingPlanId)
            VALUES
                ('user-a', 'Pull-ups', 'Plan A', 3, 0, 0, 0, 4, (SELECT Id FROM TrainingPlans WHERE WorkoutName = 'Plan A')),
                ('user-b', 'Push-ups', 'Plan B', 3, 0, 0, 0, 4, (SELECT Id FROM TrainingPlans WHERE WorkoutName = 'Plan B'));
            """);

        await db.Database.MigrateAsync();

        var plans = await db.TrainingPlans
            .AsNoTracking()
            .ToDictionaryAsync(x => x.UserId!, x => x.RestBetweenExercisesSeconds);
        var exercises = await db.Exercises
            .AsNoTracking()
            .ToDictionaryAsync(x => x.UserId!, x => x.RestBetweenSetsSeconds);
        Assert.Equal(150, plans["user-a"]);
        Assert.Equal(75, exercises["user-a"]);
        Assert.Equal(120, plans["user-b"]);
        Assert.Equal(90, exercises["user-b"]);
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
