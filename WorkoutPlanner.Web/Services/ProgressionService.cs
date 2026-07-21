namespace WorkoutPlanner.Web.Services;

public static class ProgressionService
{
    private static readonly int[] DefaultBaseReps = [8, 7, 6];

    public static string GetReps(int level)
    {
        return GetReps(null, level);
    }

    public static string GetReps(string? baseReps, int level)
    {
        var reps = ParseBaseReps(baseReps);

        return string.Join(
            ",",
            reps.Select(rep => rep + level));
    }

    public static bool TryNormalizeBaseReps(
        string? input,
        out string normalized)
    {
        var parts =
            (input ?? "")
            .Split(
                [',', ';', ' '],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (parts.Length != 3)
        {
            normalized = "";
            return false;
        }

        var reps = new int[3];

        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out reps[i]) ||
                reps[i] <= 0)
            {
                normalized = "";
                return false;
            }
        }

        normalized = string.Join(",", reps);
        return true;
    }

    private static int[] ParseBaseReps(string? baseReps)
    {
        return TryNormalizeBaseReps(
            baseReps,
            out var normalized)
            ? normalized
                .Split(',')
                .Select(int.Parse)
                .ToArray()
            : DefaultBaseReps;
    }
}
