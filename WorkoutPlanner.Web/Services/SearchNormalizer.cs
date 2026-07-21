using System.Text.RegularExpressions;

namespace WorkoutPlanner.Web.Services;

public static class SearchNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.ToLowerInvariant();

        text = text.Replace('ё', 'е');

        text = Regex.Replace(text, @"[^\p{L}\p{N}\s]", "");

        text = Regex.Replace(text, @"\s+", " ");

        return text.Trim();
    }
}
