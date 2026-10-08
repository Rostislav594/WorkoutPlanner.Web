using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class ExerciseLibrarySeederTests
{
    [Fact]
    public async Task Seed_EmptyDatabase_CreatesWholeCatalog()
    {
        await using var connection = await OpenAsync();
        await using var db = await CreateMigratedAsync(connection);

        ExerciseLibrarySeeder.Seed(db);

        Assert.Equal(ExerciseLibraryCatalog.Muscles.Count, await db.Muscles.CountAsync());
        Assert.Equal(ExerciseLibraryCatalog.Exercises.Count, await db.ExerciseDefinitions.CountAsync());
        Assert.Equal(
            ExerciseLibraryCatalog.Exercises.Sum(x => x.SecondaryMuscles.Count),
            await db.ExerciseSecondaryMuscles.CountAsync());

        var deadlift = await db.ExerciseDefinitions
            .Include(x => x.PrimaryMuscle)
            .Include(x => x.SecondaryMuscles)
            .SingleAsync(x => x.Name == "Становая тяга");
        Assert.Equal("Deadlift", deadlift.NameEn);
        Assert.Equal("Станова тяга", deadlift.NameUk);
        Assert.Equal(MuscleBodyPart.Back, deadlift.PrimaryMuscle!.BodyPart);
        Assert.Equal(5, deadlift.SecondaryMuscles.Count);
    }

    [Fact]
    public async Task Seed_SecondRun_AddsNothing()
    {
        await using var connection = await OpenAsync();
        await using var db = await CreateMigratedAsync(connection);

        ExerciseLibrarySeeder.Seed(db);
        ExerciseLibrarySeeder.Seed(db);

        Assert.Equal(ExerciseLibraryCatalog.Muscles.Count, await db.Muscles.CountAsync());
        Assert.Equal(ExerciseLibraryCatalog.Exercises.Count, await db.ExerciseDefinitions.CountAsync());
    }

    [Fact]
    public async Task Seed_ExistingLibrary_AddsMissingAndKeepsExistingCoefficients()
    {
        await using var connection = await OpenAsync();
        await using var db = await CreateMigratedAsync(connection);

        // Первая партия библиотеки: русские названия без переводов и свой коэффициент,
        // который синхронизация не должна перезаписать.
        var chest = new Muscle { Name = "Грудные мышцы" };
        var triceps = new Muscle { Name = "Трицепс" };
        db.Muscles.AddRange(chest, triceps);
        var bench = new ExerciseDefinition
        {
            Name = "Жим лёжа",
            PrimaryMuscle = chest,
            ExerciseCoefficient = 1.5,
            SecondaryMuscles = [new ExerciseSecondaryMuscle { Muscle = triceps, Coefficient = 0.2 }]
        };
        db.ExerciseDefinitions.Add(bench);
        await db.SaveChangesAsync();
        var benchId = bench.Id;
        db.ChangeTracker.Clear();

        ExerciseLibrarySeeder.Seed(db);

        var stored = await db.ExerciseDefinitions
            .Include(x => x.SecondaryMuscles)
            .SingleAsync(x => x.Name == "Жим лёжа");
        Assert.Equal(benchId, stored.Id);
        Assert.Equal(1.5, stored.ExerciseCoefficient);
        Assert.Equal(0.2, Assert.Single(stored.SecondaryMuscles).Coefficient);
        Assert.Equal("Жим лежачи", stored.NameUk);
        Assert.Equal("Barbell bench press", stored.NameEn);

        var storedChest = await db.Muscles.SingleAsync(x => x.Name == "Грудные мышцы");
        Assert.Equal("Chest", storedChest.NameEn);
        Assert.Equal(MuscleBodyPart.Chest, storedChest.BodyPart);
        Assert.Equal(ExerciseLibraryCatalog.Exercises.Count, await db.ExerciseDefinitions.CountAsync());
    }

    private static async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<WorkoutDbContext> CreateMigratedAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<WorkoutDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new WorkoutDbContext(options);
        await db.Database.MigrateAsync();
        return db;
    }
}
