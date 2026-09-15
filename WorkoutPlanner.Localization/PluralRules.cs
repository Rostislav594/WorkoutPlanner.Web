using System.Globalization;

namespace WorkoutPlanner.Localization;

/// <summary>Форма множественного числа по классификации CLDR.</summary>
public enum PluralCategory
{
    One,
    Few,
    Many
}

/// <summary>
/// Выбор формы множественного числа для поддерживаемых языков.
///
/// Это главная причина, по которой нельзя склеивать строки вручную:
/// в русском и украинском три формы (1 подход / 2 подхода / 5 подходов),
/// а в английском две (1 set / 2 sets). Конкатенация вида $"{n} подхода"
/// ломается на первом же числе и не переводится в принципе.
/// </summary>
public static class PluralRules
{
    public static PluralCategory Select(string languageCode, int count)
    {
        var absolute = count == int.MinValue ? int.MaxValue : Math.Abs(count);

        return AppLanguages.Resolve(languageCode) switch
        {
            // Английский: одна форма для 1, вторая для всего остального.
            AppLanguages.English => absolute == 1 ? PluralCategory.One : PluralCategory.Many,

            // Русский и украинский делят одно правило CLDR.
            _ => SelectEastSlavic(absolute)
        };
    }

    public static PluralCategory Select(CultureInfo culture, int count) =>
        Select(culture.TwoLetterISOLanguageName, count);

    private static PluralCategory SelectEastSlavic(int count)
    {
        var lastDigit = count % 10;
        var lastTwoDigits = count % 100;

        // 1, 21, 31... но не 11: подход, тренировка
        if (lastDigit == 1 && lastTwoDigits != 11)
            return PluralCategory.One;

        // 2-4, 22-24... но не 12-14: подхода, тренировки
        if (lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14)
            return PluralCategory.Few;

        // 0, 5-20, 25-30...: подходов, тренировок
        return PluralCategory.Many;
    }
}
