namespace WorkoutPlanner.Web.Models;

public class Muscle
{
    public int Id { get; set; }

    /// <summary>Русское название: оно же ключ мышцы в каталоге библиотеки.</summary>
    public string Name { get; set; } = string.Empty;

    public string NameUk { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>Часть тела, по которой телефон группирует упражнения в окне выбора.</summary>
    public MuscleBodyPart BodyPart { get; set; }

    public ICollection<ExerciseSecondaryMuscle> SecondaryMuscleLinks { get; set; } = [];

    public string NameFor(string language) =>
        LibraryNames.For(language, Name, NameUk, NameEn);
}
