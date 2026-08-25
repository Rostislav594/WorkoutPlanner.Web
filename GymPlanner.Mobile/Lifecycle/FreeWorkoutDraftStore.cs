using System.Text.Json;
using GymPlanner.Mobile.Authentication;

namespace GymPlanner.Mobile.Lifecycle;

public sealed class FreeWorkoutDraftStore(MobileAuthenticationService authentication)
{
    private const string KeyPrefix = "gymplanner.free-workout.v1.";

    public bool HasActiveDraft => Load() is { IsActive: true };

    public FreeWorkoutDraft? Load()
    {
        try
        {
            var json = Preferences.Default.Get(GetKey(), string.Empty);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<FreeWorkoutDraft>(json);
        }
        catch (JsonException)
        {
            Preferences.Default.Remove(GetKey());
            return null;
        }
    }

    public void Save(FreeWorkoutDraft draft) => Preferences.Default.Set(GetKey(), JsonSerializer.Serialize(draft));
    public void Clear() => Preferences.Default.Remove(GetKey());

    private string GetKey() => KeyPrefix + Uri.EscapeDataString((authentication.CurrentEmail ?? "anonymous").Trim().ToLowerInvariant());
}

public sealed record FreeWorkoutDraft(bool IsActive, List<FreeWorkoutExerciseDraft> Exercises);
public sealed record FreeWorkoutExerciseDraft(int Id, string Name, int? ExerciseDefinitionId, string Status, int? SupersetGroupId, List<FreeWorkoutSetDraft> Sets);
public sealed record FreeWorkoutSetDraft(int SetNumber, int Repetitions, double Weight, bool Completed, bool IsWarmup);
