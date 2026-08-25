namespace WorkoutPlanner.Web.Application.Contracts;

public sealed class UserProfile
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string Gender { get; set; } = string.Empty;
    public int RestBetweenSetsSeconds { get; set; } = 90;
    public int RestBetweenExercisesSeconds { get; set; } = 120;
}

public sealed record ProfileUpdateRequest(
    string FirstName,
    string LastName,
    DateTime? BirthDate,
    string Gender);

public sealed record RestTimerSettingsUpdateRequest(
    int RestBetweenSetsSeconds,
    int RestBetweenExercisesSeconds);

public sealed record PasswordChangeRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record OperationResult(
    bool Succeeded,
    IReadOnlyList<string> Errors)
{
    public static OperationResult Success { get; } = new(true, []);
    public static OperationResult Failure(params string[] errors) =>
        new(false, errors);
}

public sealed record PhotoUpload(
    Stream Content,
    string ContentType,
    long Length);

public sealed record PhotoDownload(
    Stream Content,
    string ContentType,
    long Length);

public enum ExercisePhotoMutationFailure
{
    None,
    ExerciseNotFound,
    InvalidFile,
    StorageFailure
}

public sealed record ExercisePhotoMutationResult(
    bool Succeeded,
    ExercisePhotoMutationFailure Failure);
