using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Data;

/// <summary>
/// Приводит библиотеку в базе к <see cref="ExerciseLibraryCatalog"/> при каждом старте сервера.
/// </summary>
/// <remarks>
/// Недостающие мышцы и упражнения добавляются, у существующих обновляются только
/// переводы и часть тела. Коэффициенты и вторичные мышцы уже существующих
/// упражнений не меняются: по ним посчитан прогресс, и правка задним числом
/// изменила бы старые графики. Записи сопоставляются по русскому названию.
/// </remarks>
public static class ExerciseLibrarySeeder
{
    public static void Seed(WorkoutDbContext db)
    {
        var muscles = SyncMuscles(db);
        SyncExercises(db, muscles);
    }

    private static Dictionary<string, Muscle> SyncMuscles(WorkoutDbContext db)
    {
        var muscles = db.Muscles.ToDictionary(x => x.Name);

        foreach (var entry in ExerciseLibraryCatalog.Muscles)
        {
            if (!muscles.TryGetValue(entry.Name, out var muscle))
            {
                muscle = new Muscle { Name = entry.Name };
                db.Muscles.Add(muscle);
                muscles.Add(entry.Name, muscle);
            }

            muscle.NameUk = entry.NameUk;
            muscle.NameEn = entry.NameEn;
            muscle.BodyPart = entry.BodyPart;
        }

        db.SaveChanges();
        return muscles;
    }

    private static void SyncExercises(
        WorkoutDbContext db,
        IReadOnlyDictionary<string, Muscle> muscles)
    {
        var definitions = db.ExerciseDefinitions.ToDictionary(x => x.Name);

        foreach (var entry in ExerciseLibraryCatalog.Exercises)
        {
            if (definitions.TryGetValue(entry.Name, out var definition))
            {
                definition.NameUk = entry.NameUk;
                definition.NameEn = entry.NameEn;
                continue;
            }

            db.ExerciseDefinitions.Add(new ExerciseDefinition
            {
                Name = entry.Name,
                NameUk = entry.NameUk,
                NameEn = entry.NameEn,
                PrimaryMuscle = muscles[entry.PrimaryMuscle],
                ExerciseCoefficient = entry.Coefficient,
                SecondaryMuscles = entry.SecondaryMuscles
                    .Select(x => new ExerciseSecondaryMuscle
                    {
                        Muscle = muscles[x.Muscle],
                        Coefficient = x.Coefficient
                    })
                    .ToList()
            });
        }

        db.SaveChanges();
    }
}
