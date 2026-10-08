using System.Security.Cryptography;
using System.Text;

namespace GymPlanner.Mobile.Offline;

/// <summary>Имена документов офлайн-хранилища.</summary>
public static class OfflineKeys
{
    public const string Plans = "plans";
    public const string Calendar = "calendar";
    public const string History = "history";
    public const string Profile = "profile";
    public const string WelcomeGuide = "welcome-guide";
    public const string ExerciseDefinitions = "exercise-definitions";
    public const string FreeWorkoutDraft = "free-draft";
    public const string ProgressOverview = "progress-overview";
    public const string ProgressPrefix = "progress-";
    public const string InboxMessages = "inbox-messages";
    public const string InboxUnreadCount = "inbox-unread";

    // Названия упражнений приходят на языке интерфейса: после смены языка
    // без связи библиотека не должна остаться на прежнем.
    public static string ExerciseDefinitionsIn(string language) => $"{ExerciseDefinitions}-{language}";

    public static string WorkoutProgress(int trainingPlanId) => $"progress-workout-{trainingPlanId}";

    public static string WorkoutExercises(int trainingPlanId) => $"progress-exercises-{trainingPlanId}";

    public static string ExerciseProgress(int trainingPlanId, string exerciseName) =>
        $"progress-exercise-{trainingPlanId}-{Hash(exerciseName)}";

    public static string ExercisePhoto(int exerciseId) => $"photo-{exerciseId}";

    public static string ExercisePhotoType(int exerciseId) => $"photo-type-{exerciseId}";

    // Имя упражнения — произвольный текст, в имени файла ему не место.
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)), 0, 10).ToLowerInvariant();
}
