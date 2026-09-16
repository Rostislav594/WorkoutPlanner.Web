using System.Globalization;
using System.Resources;
using WorkoutPlanner.Localization;

namespace WorkoutPlanner.Web.Tests.Unit;

public class PluralRulesTests
{
    // Русский и украинский: 1 подход / 2 подхода / 5 подходов.
    [Theory]
    [InlineData(1, PluralCategory.One)]
    [InlineData(21, PluralCategory.One)]
    [InlineData(101, PluralCategory.One)]
    [InlineData(11, PluralCategory.Many)]   // исключение: одиннадцать
    [InlineData(111, PluralCategory.Many)]
    [InlineData(2, PluralCategory.Few)]
    [InlineData(4, PluralCategory.Few)]
    [InlineData(23, PluralCategory.Few)]
    [InlineData(12, PluralCategory.Many)]   // исключение: двенадцать-четырнадцать
    [InlineData(14, PluralCategory.Many)]
    [InlineData(0, PluralCategory.Many)]
    [InlineData(5, PluralCategory.Many)]
    [InlineData(100, PluralCategory.Many)]
    public void EastSlavicLanguagesFollowCldrRules(int count, PluralCategory expected)
    {
        Assert.Equal(expected, PluralRules.Select(AppLanguages.Russian, count));
        Assert.Equal(expected, PluralRules.Select(AppLanguages.Ukrainian, count));
    }

    [Theory]
    [InlineData(1, PluralCategory.One)]
    [InlineData(0, PluralCategory.Many)]
    [InlineData(2, PluralCategory.Many)]
    [InlineData(11, PluralCategory.Many)]
    [InlineData(21, PluralCategory.Many)]
    public void EnglishHasTwoForms(int count, PluralCategory expected) =>
        Assert.Equal(expected, PluralRules.Select(AppLanguages.English, count));

    [Fact]
    public void NegativeCountsUseTheSameFormAsPositive() =>
        Assert.Equal(
            PluralRules.Select(AppLanguages.Russian, 2),
            PluralRules.Select(AppLanguages.Russian, -2));
}

public class AppLanguagesTests
{
    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("uk-UA", "uk")]
    [InlineData("en-GB", "en")]
    [InlineData("en_US", "en")]
    [InlineData("uk-UA;q=0.8", "uk")]
    [InlineData("ru", "ru")]
    public void ResolveStripsRegionAndQuality(string tag, string expected) =>
        Assert.Equal(expected, AppLanguages.Resolve(tag));

    [Theory]
    [InlineData("de")]
    [InlineData("pl-PL")]
    [InlineData("")]
    [InlineData(null)]
    public void UnsupportedTagsFallBackToRussian(string? tag) =>
        Assert.Equal(AppLanguages.Default, AppLanguages.Resolve(tag));

    [Fact]
    public void ResolveFromPreferencesPicksFirstSupported() =>
        Assert.Equal("uk", AppLanguages.ResolveFromPreferences(["de-DE", "pl", "uk-UA", "en"]));

    [Fact]
    public void ResolveFromPreferencesFallsBackWhenNothingMatches() =>
        Assert.Equal(AppLanguages.Default, AppLanguages.ResolveFromPreferences(["de", "pl"]));
}

public class AppTextTests
{
    [Theory]
    [InlineData(AppLanguages.Russian, "Выберите язык интерфейса", "Язык")]
    [InlineData(AppLanguages.Ukrainian, "Оберіть мову інтерфейсу", "Мова")]
    [InlineData(AppLanguages.English, "Choose the interface language", "Language")]
    public void ResolvesStringsForTheRequestedLanguage(
        string language, string descriptionPrefix, string title)
    {
        var text = new AppText(() => language);

        Assert.Equal(language, text.Language);
        Assert.StartsWith(descriptionPrefix, text["Profile_Language_Description"]);
        Assert.Equal(title, text["Profile_Language_Title"]);
    }

    /// <summary>
    /// Ключевой инвариант. На устройстве выяснилось, что в MAUI BlazorWebView
    /// культура потока рендерера фиксируется при старте процесса: после смены
    /// языка ни перерисовка, ни перезагрузка WebView текст не меняли.
    /// Поэтому AppText обязан игнорировать окружение потока и брать язык
    /// только из своего провайдера.
    /// </summary>
    [Fact]
    public void IgnoresAmbientThreadCulture()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(AppLanguages.Russian);
            var text = new AppText(() => AppLanguages.English);

            Assert.Equal("Language", text["Profile_Language_Title"]);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void SwitchingLanguageTakesEffectImmediately()
    {
        var language = AppLanguages.Russian;
        var text = new AppText(() => language);

        Assert.Equal("Язык", text["Profile_Language_Title"]);

        language = AppLanguages.Ukrainian;
        Assert.Equal("Мова", text["Profile_Language_Title"]);
    }

    [Theory]
    [InlineData(AppLanguages.Russian, 1, "1 подход")]
    [InlineData(AppLanguages.Russian, 3, "3 подхода")]
    [InlineData(AppLanguages.Russian, 5, "5 подходов")]
    [InlineData(AppLanguages.Russian, 11, "11 подходов")]
    [InlineData(AppLanguages.Ukrainian, 1, "1 підхід")]
    [InlineData(AppLanguages.Ukrainian, 3, "3 підходи")]
    [InlineData(AppLanguages.Ukrainian, 5, "5 підходів")]
    [InlineData(AppLanguages.English, 1, "1 set")]
    [InlineData(AppLanguages.English, 3, "3 sets")]
    public void PluralPicksTheRightForm(string language, int count, string expected) =>
        Assert.Equal(expected, new AppText(() => language).Plural("Sets", count));

    [Fact]
    public void UnknownKeyReturnsTheKeyInsteadOfThrowing() =>
        Assert.Equal("Nope_Missing", new AppText(() => AppLanguages.Russian)["Nope_Missing"]);
}

public class ResourceCoverageTests
{
    private static readonly string[] Translations = [AppLanguages.Ukrainian, AppLanguages.English];

    /// <summary>
    /// Ключи, чей перевод законно совпадает с русским оригиналом.
    /// Список намеренно явный: добавление сюда — осознанное решение,
    /// а не способ заглушить упавший тест.
    /// </summary>
    private static readonly HashSet<(string Resource, string Language, string Key)> IdenticalByDesign =
    [
        // Слова, которые в украинском пишутся так же, как в русском.
        (nameof(AppStrings), AppLanguages.Ukrainian, "Common_Back"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Common_Password"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Common_Done"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Common_Kg"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Common_RepsShort"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Common_Superset"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "History_SupersetLabel"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Difficulty_Easy"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Today_Pause"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Chart_ColumnDate"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Help_ChartDateAxis"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Picker_Selected"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Web_Rate_Easy"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Web_Profile_Model"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Admin_Column_Status"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Admin_Field_Platform"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Admin_Field_Os"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Admin_Publications_Editor"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Admin_Publications_Type"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Admin_Publications_TitleLabel"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Admin_Publications_BodyLabel"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Server_Starter_Upper1"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Server_Starter_Lower1"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Server_Starter_Upper2"),
        (nameof(AppStrings), AppLanguages.Ukrainian, "Server_Starter_Lower2"),

        // «✓ {0}» — отметка выбранного пункта, она одинакова во всех языках.
        (nameof(AppStrings), AppLanguages.English, "Picker_Selected")
    ];

    public static TheoryData<string, string> ResourceSets()
    {
        var data = new TheoryData<string, string>();
        foreach (var resource in new[] { nameof(AppStrings), nameof(ApiErrors) })
            foreach (var language in Translations)
                data.Add(resource, language);
        return data;
    }

    /// <summary>
    /// Каждый ключ русского набора обязан существовать в украинском и английском.
    /// Без этой проверки недостающий перевод молча падает обратно на русский
    /// и обнаруживается только пользователем.
    /// </summary>
    [Theory]
    [MemberData(nameof(ResourceSets))]
    public void EveryRussianKeyHasATranslation(string resource, string language)
    {
        var manager = CreateManager(resource);
        var russian = ReadKeys(manager, AppLanguages.Russian);
        var translated = ReadKeys(manager, language);

        Assert.NotEmpty(russian);
        var missing = russian.Keys.Except(translated.Keys, StringComparer.Ordinal).ToArray();
        Assert.True(
            missing.Length == 0,
            $"В {resource}.{language}.resx не хватает ключей: {string.Join(", ", missing)}");
    }

    /// <summary>Перевод не должен случайно остаться копией русского текста.</summary>
    [Theory]
    [MemberData(nameof(ResourceSets))]
    public void TranslationsAreNotLeftAsRussianCopies(string resource, string language)
    {
        var manager = CreateManager(resource);
        var russian = ReadKeys(manager, AppLanguages.Russian);
        var translated = ReadKeys(manager, language);

        var untranslated = russian
            .Where(pair => translated.TryGetValue(pair.Key, out var value)
                && string.Equals(value, pair.Value, StringComparison.Ordinal)
                && !IdenticalByDesign.Contains((resource, language, pair.Key))
                // Формы мн. числа в uk иногда совпадают с ru («секунда»), это нормально.
                && !pair.Key.EndsWith("_One", StringComparison.Ordinal)
                && !pair.Key.EndsWith("_Few", StringComparison.Ordinal)
                && !pair.Key.EndsWith("_Many", StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray();

        Assert.True(
            untranslated.Length == 0,
            $"В {resource}.{language}.resx остался русский текст: {string.Join(", ", untranslated)}");
    }

    [Fact]
    public void EveryErrorCodeHasAMessage()
    {
        var manager = CreateManager(nameof(ApiErrors));
        var messages = ReadKeys(manager, AppLanguages.Russian);

        var codes = typeof(ApiErrorCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Where(field => field.Name != nameof(ApiErrorCodes.ExtensionName))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        var missing = codes.Except(messages.Keys, StringComparer.Ordinal).ToArray();
        Assert.True(
            missing.Length == 0,
            $"Коды без текста в ApiErrors.resx: {string.Join(", ", missing)}");
    }

    private static ResourceManager CreateManager(string resource) =>
        new($"{typeof(AppStrings).Namespace}.{resource}", typeof(AppStrings).Assembly);

    private static Dictionary<string, string> ReadKeys(ResourceManager manager, string language)
    {
        // Русский объявлен нейтральным языком (NeutralResourcesLanguage), поэтому его
        // строки лежат в основной сборке под инвариантной культурой, а не под "ru".
        // Сателлитные uk/en читаются по своей культуре.
        // tryParents: false принципиально — иначе отсутствующий перевод молча
        // подменится русским и тест ничего не поймает.
        var culture = language == AppLanguages.Default
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(language);
        using var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"Нет набора ресурсов для {language}.");

        return set.Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Value is string)
            .ToDictionary(
                entry => (string)entry.Key,
                entry => (string)entry.Value!,
                StringComparer.Ordinal);
    }
}
