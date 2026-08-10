using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using GymPlanner.Mobile.Photos;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class ExercisePhotoApiClient(HttpClient client) : IExercisePhotoApiClient
{
    public async Task<ApiResult<ExercisePhotoApiResponse>> UploadAsync(
        int exerciseId,
        PickedPhoto photo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var file = new ByteArrayContent(photo.Content);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse(photo.ContentType);
            using var multipart = new MultipartFormDataContent();
            multipart.Add(file, "file", GetSafeUploadFileName(photo.ContentType));
            using var response = await client.PostAsync(
                $"api/v1/exercises/{exerciseId}/photo",
                multipart,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    null,
                    await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            }

            var value = await response.Content.ReadFromJsonAsync<
                ExercisePhotoApiResponse>(cancellationToken);
            return value is null
                ? ApiResult<ExercisePhotoApiResponse>.Failure(
                    "Сервер не подтвердил сохранение фотографии.")
                : ApiResult<ExercisePhotoApiResponse>.Success(value);
        }
        catch (HttpRequestException)
        {
            return ApiResult<ExercisePhotoApiResponse>.Failure(
                "Нет соединения с сервером.");
        }
    }

    public async Task<OptionalApiResult<ExercisePhotoContent>> DownloadAsync(
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync(
                $"api/v1/exercises/{exerciseId}/photo",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return OptionalApiResult<ExercisePhotoContent>.Empty();
            if (!response.IsSuccessStatusCode)
            {
                return OptionalApiResult<ExercisePhotoContent>.Failure(
                    [.. await MobileApiErrorReader.ReadAsync(response, cancellationToken)]);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
            {
                return OptionalApiResult<ExercisePhotoContent>.Failure(
                    "Сервер вернул неподдерживаемый формат фотографии.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return OptionalApiResult<ExercisePhotoContent>.Success(
                new ExercisePhotoContent(contentType, bytes));
        }
        catch (HttpRequestException)
        {
            return OptionalApiResult<ExercisePhotoContent>.Failure(
                "Нет соединения с сервером.");
        }
    }

    public async Task<ApiResult> DeleteAsync(
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.DeleteAsync(
                $"api/v1/exercises/{exerciseId}/photo",
                cancellationToken);
            return response.IsSuccessStatusCode
                ? ApiResult.Success
                : new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failure("Нет соединения с сервером.");
        }
    }

    private static string GetSafeUploadFileName(string contentType) =>
        contentType switch
        {
            "image/png" => "photo.png",
            "image/webp" => "photo.webp",
            _ => "photo.jpg"
        };
}
