namespace WorkoutPlanner.Web.Models;

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;   

public class ExerciseDefinition
{
    public int Id { get; set; }

    /// <summary>Русское название: оно же ключ упражнения в каталоге библиотеки.</summary>
    public string Name { get; set; } = string.Empty;

    public string NameUk { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string SearchName { get; set; } = string.Empty;

    public int PrimaryMuscleId { get; set; }

    public Muscle? PrimaryMuscle { get; set; }

    public double ExerciseCoefficient { get; set; }

    public ExerciseType Type { get; set; }

    public ICollection<ExerciseSecondaryMuscle> SecondaryMuscles
    {
        get;
        set;
    }
      = [];

    public string NameFor(string language) =>
        LibraryNames.For(language, Name, NameUk, NameEn);

    public string NormalizedName =>
    Normalize(Name);

    private static string Normalize(string value)
    {
        return Regex.Replace(
                value
                    .ToLowerInvariant()
                    .Replace('ё', 'е')
                    .Trim(),
                @"\s+",
                " ");
    }
}