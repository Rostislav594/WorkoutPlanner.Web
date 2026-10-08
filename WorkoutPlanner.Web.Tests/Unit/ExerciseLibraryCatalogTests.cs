using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class ExerciseLibraryCatalogTests
{
    public static TheoryData<string> Languages => new() { "ru", "uk", "en" };

    [Theory]
    [MemberData(nameof(Languages))]
    public void ExerciseNames_AreFilledAndUniquePerLanguage(string language)
    {
        var names = ExerciseLibraryCatalog.Exercises
            .Select(x => NameOf(x, language))
            .ToList();

        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        var duplicates = names
            .GroupBy(x => x.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Exercises_ReferenceOnlyCatalogMuscles()
    {
        var muscles = ExerciseLibraryCatalog.Muscles.Select(x => x.Name).ToHashSet();

        Assert.All(ExerciseLibraryCatalog.Exercises, exercise =>
        {
            Assert.Contains(exercise.PrimaryMuscle, muscles);
            Assert.All(exercise.SecondaryMuscles, secondary =>
            {
                Assert.Contains(secondary.Muscle, muscles);
                Assert.NotEqual(exercise.PrimaryMuscle, secondary.Muscle);
            });
            Assert.Equal(
                exercise.SecondaryMuscles.Count,
                exercise.SecondaryMuscles.Select(x => x.Muscle).Distinct().Count());
        });
    }

    [Fact]
    public void Coefficients_StayInTheLibraryScale()
    {
        Assert.All(ExerciseLibraryCatalog.Exercises, exercise =>
        {
            Assert.InRange(exercise.Coefficient, 1.4, 2.0);
            Assert.All(exercise.SecondaryMuscles, secondary =>
                Assert.InRange(secondary.Coefficient, 0.05, 0.7));
        });
    }

    [Fact]
    public void Muscles_HaveTranslationsAndBodyPart()
    {
        Assert.All(ExerciseLibraryCatalog.Muscles, muscle =>
        {
            Assert.False(string.IsNullOrWhiteSpace(muscle.NameUk));
            Assert.False(string.IsNullOrWhiteSpace(muscle.NameEn));
            Assert.NotEqual(MuscleBodyPart.Other, muscle.BodyPart);
        });
    }

    [Fact]
    public void UkrainianNames_HaveNoRussianOnlyLetters()
    {
        var names = ExerciseLibraryCatalog.Exercises.Select(x => x.NameUk)
            .Concat(ExerciseLibraryCatalog.Muscles.Select(x => x.NameUk));

        Assert.All(names, name => Assert.DoesNotMatch("[ыэъёЫЭЪЁ]", name));
    }

    [Theory]
    [InlineData("uk", "Жим лежачи")]
    [InlineData("en", "Barbell bench press")]
    [InlineData("ru", "Жим лёжа")]
    [InlineData("de", "Жим лёжа")]
    public void NameFor_PicksLanguage_AndFallsBackToRussian(string language, string expected)
    {
        var definition = new ExerciseDefinition
        {
            Name = "Жим лёжа",
            NameUk = "Жим лежачи",
            NameEn = "Barbell bench press"
        };

        Assert.Equal(expected, definition.NameFor(language));
    }

    [Fact]
    public void NameFor_WithoutTranslation_ShowsRussian()
    {
        var definition = new ExerciseDefinition { Name = "Своё упражнение" };

        Assert.Equal("Своё упражнение", definition.NameFor("en"));
    }

    private static string NameOf(ExerciseLibraryCatalog.ExerciseEntry entry, string language) =>
        language switch
        {
            "uk" => entry.NameUk,
            "en" => entry.NameEn,
            _ => entry.Name
        };
}
