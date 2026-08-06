namespace WorkoutPlanner.Web.Api.Contracts;

public sealed record ExercisePhotoApiResponse(
    int ExerciseId,
    string DownloadUrl);
