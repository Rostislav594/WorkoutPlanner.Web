namespace WorkoutPlanner.Web.Models;

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;   

public class ExerciseDefinition
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

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