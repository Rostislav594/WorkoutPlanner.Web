using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Offline;
using GymPlanner.Mobile.Photos;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Фото упражнений: скачанные сохраняются на телефоне и видны без связи;
/// загрузка и удаление — только онлайн.
/// </summary>
public sealed class OfflineExercisePhotoApiClient(ExercisePhotoApiClient inner, OfflineRuntime runtime) : IExercisePhotoApiClient
{
    public sealed record StoredPhotoType(string ContentType);

    public async Task<ApiResult<ExercisePhotoApiResponse>> UploadAsync(
        int exerciseId,
        PickedPhoto photo,
        CancellationToken cancellationToken = default) =>
        await runtime.WriteAsync(
            token => inner.UploadAsync(exerciseId, photo, token),
            _ => ForgetAsync(exerciseId, cancellationToken),
            cancellationToken);

    public Task<OptionalApiResult<ExercisePhotoContent>> DownloadAsync(
        int exerciseId,
        CancellationToken cancellationToken = default) =>
        runtime.ReadOptionalAsync(
            token => inner.DownloadAsync(exerciseId, token),
            async () =>
            {
                var stored = await runtime.Store.GetAsync<OfflineOptional<StoredPhotoType>>(
                    OfflineKeys.ExercisePhotoType(exerciseId),
                    cancellationToken);
                if (stored is null)
                    return (false, null);
                if (stored.Value is null)
                    return (true, null);

                var bytes = await runtime.Store.ReadBytesAsync(OfflineKeys.ExercisePhoto(exerciseId), cancellationToken);
                return bytes is null
                    ? (false, null)
                    : (true, new ExercisePhotoContent(stored.Value.ContentType, bytes));
            },
            async photo =>
            {
                if (photo is not null)
                    await runtime.Store.WriteBytesAsync(OfflineKeys.ExercisePhoto(exerciseId), photo.Content, cancellationToken);
                else
                    await runtime.Store.RemoveBytesAsync(OfflineKeys.ExercisePhoto(exerciseId), cancellationToken);

                await runtime.Store.SetAsync(
                    OfflineKeys.ExercisePhotoType(exerciseId),
                    new OfflineOptional<StoredPhotoType>(photo is null ? null : new StoredPhotoType(photo.ContentType)),
                    cancellationToken);
            },
            cancellationToken);

    public Task<ApiResult> DeleteAsync(int exerciseId, CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            token => inner.DeleteAsync(exerciseId, token),
            () => ForgetAsync(exerciseId, cancellationToken),
            cancellationToken);

    // Новое фото перечитается при следующем показе; до этого копия не должна показывать старое.
    private async Task ForgetAsync(int exerciseId, CancellationToken cancellationToken)
    {
        await runtime.Store.RemoveBytesAsync(OfflineKeys.ExercisePhoto(exerciseId), cancellationToken);
        await runtime.Store.RemoveAsync(OfflineKeys.ExercisePhotoType(exerciseId), cancellationToken);
    }
}
