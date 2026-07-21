using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;

namespace WorkoutPlanner.Web.Services;

public static class LibraryValidator
{
    public static void Validate(WorkoutDbContext db)
    {
        ValidatePrimaryMuscles(db);
        ValidateDuplicateExercises(db);
        ValidateSecondaryMuscles(db);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Exercise Library validation passed.");
        Console.ResetColor();
    }

    private static void ValidatePrimaryMuscles(
        WorkoutDbContext db)
    {
        var exercises =
            db.ExerciseDefinitions
              .Where(x => x.PrimaryMuscleId == 0)
              .ToList();

        if (exercises.Any())
        {
            throw new Exception(
                $"Exercises without primary muscle: {string.Join(", ", exercises.Select(x => x.Name))}");
        }
    }

    private static void ValidateDuplicateExercises(
        WorkoutDbContext db)
    {
        var duplicates =
            db.ExerciseDefinitions
              .Select(x => x.Name)
              .AsEnumerable()
              .GroupBy(name => name)
              .Where(g => g.Count() > 1)
              .ToList();

        if (duplicates.Any())
        {
            throw new Exception(
                $"Duplicate exercise names: {string.Join(", ", duplicates.Select(x => x.Key))}");
        }
    }

    private static void ValidateSecondaryMuscles(
        WorkoutDbContext db)
    {
        var duplicates =
            db.ExerciseSecondaryMuscles
              .Select(x => new
              {
                  x.ExerciseDefinitionId,
                  x.MuscleId
              })
              .AsEnumerable()
              .GroupBy(x => new
              {
                  x.ExerciseDefinitionId,
                  x.MuscleId
              })
              .Where(x => x.Count() > 1)
              .ToList();

        if (duplicates.Any())
        {
            throw new Exception(
                "Duplicate secondary muscles found.");
        }
    }
}
