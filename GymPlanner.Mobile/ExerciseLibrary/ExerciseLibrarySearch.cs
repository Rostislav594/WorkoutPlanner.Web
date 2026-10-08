using System.Globalization;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.ExerciseLibrary;

/// <summary>
/// Поиск и разделы библиотеки для окна выбора упражнения.
/// </summary>
public static class ExerciseLibrarySearch
{
    public const string Other = "other";

    /// <summary>Порядок разделов в окне: как идут группы мышц в зале, сверху вниз.</summary>
    public static IReadOnlyList<string> BodyPartOrder { get; } =
        ["chest", "back", "shoulders", "arms", "legs", "glutes", "core", Other];

    public static string BodyPartOf(ExerciseDefinitionApiResponse definition) =>
        definition.BodyPart is { Length: > 0 } bodyPart && BodyPartOrder.Contains(bodyPart)
            ? bodyPart
            : Other;

    /// <summary>
    /// Слова запроса ищутся в названии в любом порядке и по началу или середине
    /// слова: «тяга гант» находит «Тяга гантели одной рукой».
    /// </summary>
    public static IReadOnlyList<ExerciseLibraryGroup> Filter(
        IReadOnlyList<ExerciseDefinitionApiResponse> definitions,
        string? query,
        string? bodyPart)
    {
        var terms = Terms(query);
        return definitions
            .Where(x => bodyPart is null || BodyPartOf(x) == bodyPart)
            .Select(x => (Definition: x, Matches: MatchRanges(x.Name, terms)))
            .Where(x => terms.Count == 0 || x.Matches is not null)
            .GroupBy(x => BodyPartOf(x.Definition))
            .OrderBy(x => IndexOf(x.Key))
            .Select(group => new ExerciseLibraryGroup(
                group.Key,
                group
                    .OrderBy(x => x.Definition.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new ExerciseLibraryItem(x.Definition, x.Matches ?? []))
                    .ToList()))
            .ToList();
    }

    public static IReadOnlyList<string> BodyPartsIn(IReadOnlyList<ExerciseDefinitionApiResponse> definitions) =>
        definitions
            .Select(BodyPartOf)
            .Distinct()
            .OrderBy(IndexOf)
            .ToList();

    /// <summary>
    /// Делит название на куски для подсветки совпадений: <c>true</c> — совпавший кусок.
    /// </summary>
    public static IReadOnlyList<(string Text, bool IsMatch)> Highlight(
        string name,
        IReadOnlyList<ExerciseLibraryMatch> matches)
    {
        if (matches.Count == 0)
            return [(name, false)];

        var parts = new List<(string, bool)>();
        var position = 0;
        foreach (var match in matches)
        {
            if (match.Start > position)
                parts.Add((name[position..match.Start], false));
            parts.Add((name.Substring(match.Start, match.Length), true));
            position = match.Start + match.Length;
        }

        if (position < name.Length)
            parts.Add((name[position..], false));
        return parts;
    }

    private static IReadOnlyList<string> Terms(string? query) =>
        Normalize(query ?? string.Empty).Text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

    // null — хотя бы одно слово не нашлось. Иначе отсортированные непересекающиеся
    // отрезки исходного названия.
    private static IReadOnlyList<ExerciseLibraryMatch>? MatchRanges(string name, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
            return [];

        var (normalized, sourceIndex) = Normalize(name);
        var ranges = new List<(int Start, int End)>();
        foreach (var term in terms)
        {
            var index = normalized.IndexOf(term, StringComparison.Ordinal);
            if (index < 0)
                return null;
            ranges.Add((sourceIndex[index], sourceIndex[index + term.Length - 1] + 1));
        }

        var merged = new List<ExerciseLibraryMatch>();
        foreach (var range in ranges.OrderBy(x => x.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].Start + merged[^1].Length)
            {
                var last = merged[^1];
                var end = Math.Max(last.Start + last.Length, range.End);
                merged[^1] = new ExerciseLibraryMatch(last.Start, end - last.Start);
            }
            else
            {
                merged.Add(new ExerciseLibraryMatch(range.Start, range.End - range.Start));
            }
        }

        return merged;
    }

    // Без учёта регистра и с «ё» как «е». Апострофы выбрасываются («мяз» найдёт
    // «м'яз», «captains» — «Captain's»), дефис считается пробелом («хип траст»
    // найдёт «Хип-траст»). SourceIndex[i] — позиция i-го символа в исходной строке:
    // по ней подсветка попадает в исходное название.
    private static (string Text, int[] SourceIndex) Normalize(string value)
    {
        var text = new System.Text.StringBuilder(value.Length);
        var sourceIndex = new List<int>(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = char.ToLowerInvariant(value[index]);
            if (character is '\'' or 'ʼ' or '’')
                continue;

            text.Append(character switch
            {
                'ё' => 'е',
                '-' => ' ',
                _ => character
            });
            sourceIndex.Add(index);
        }

        return (text.ToString(), [.. sourceIndex]);
    }

    private static int IndexOf(string bodyPart)
    {
        for (var index = 0; index < BodyPartOrder.Count; index++)
        {
            if (BodyPartOrder[index] == bodyPart)
                return index;
        }

        return BodyPartOrder.Count;
    }
}

public sealed record ExerciseLibraryGroup(string BodyPart, IReadOnlyList<ExerciseLibraryItem> Items);

public sealed record ExerciseLibraryItem(
    ExerciseDefinitionApiResponse Definition,
    IReadOnlyList<ExerciseLibraryMatch> Matches);

public sealed record ExerciseLibraryMatch(int Start, int Length);
