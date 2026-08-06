using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Data;

public static class DbSeeder
{
    public static void Seed(WorkoutDbContext db)
    {
        SeedMuscles(db);
    }

    private static void SeedMuscles(WorkoutDbContext db)
    {
        string[] muscleNames =
        [
            "Грудные мышцы",
            "Широчайшие мышцы спины",
            "Трапециевидные мышцы",
            "Ромбовидные мышцы",
            "Разгибатели спины",
            "Передняя дельтовидная",
            "Средняя дельтовидная",
            "Задняя дельтовидная",
            "Бицепс",
            "Трицепс",
            "Предплечья",
            "Прямая мышца живота",
            "Косые мышцы живота",
            "Сгибатели бедра",
            "Квадрицепс",
            "Бицепс бедра",
            "Ягодичные мышцы",
            "Икроножные мышцы"
        ];

        var existingNames = db.Muscles
            .Select(x => x.Name)
            .ToHashSet();

        var missingMuscles = muscleNames
            .Where(name => !existingNames.Contains(name))
            .Select(name => new Muscle { Name = name });

        db.Muscles.AddRange(missingMuscles);
        db.SaveChanges();
    }
}
