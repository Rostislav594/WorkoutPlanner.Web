using GymPlanner.Mobile.Photos;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed record ExercisePhotoContent(string ContentType, byte[] Content);

public interface IExercisePhotoApiClient
{
    Task<ApiResult<ExercisePhotoApiResponse>> UploadAsync(
        int exerciseId,
        PickedPhoto photo,
        CancellationToken cancellationToken = default);

    Task<OptionalApiResult<ExercisePhotoContent>> DownloadAsync(
        int exerciseId,
        CancellationToken cancellationToken = default);

    Task<ApiResult> DeleteAsync(
        int exerciseId,
        CancellationToken cancellationToken = default);
}
