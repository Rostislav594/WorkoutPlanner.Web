using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using GymPlanner.Mobile.Photos;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class SupportApiClient(HttpClient client) : ISupportApiClient
{
    public async Task<ApiResult<SupportTicketResponse>> CreateAsync(
        string message,
        PickedPhoto? screenshot,
        SupportDeviceContext deviceContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(message), "message");
            AddOptional(multipart, "appVersion", deviceContext.AppVersion);
            AddOptional(multipart, "platform", deviceContext.Platform);
            AddOptional(multipart, "osVersion", deviceContext.OsVersion);
            AddOptional(multipart, "deviceModel", deviceContext.DeviceModel);

            if (screenshot is not null)
            {
                var file = new ByteArrayContent(screenshot.Content);
                file.Headers.ContentType = MediaTypeHeaderValue.Parse(
                    screenshot.ContentType);
                multipart.Add(
                    file,
                    "screenshot",
                    GetSafeUploadFileName(screenshot.ContentType));
            }

            using var response = await client.PostAsync(
                "api/v1/support/tickets",
                multipart,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    return ApiResult<SupportTicketResponse>.Failure(
                        "Слишком много обращений. Попробуйте отправить сообщение позже.");
                }

                if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                {
                    return ApiResult<SupportTicketResponse>.Failure(
                        "Размер скриншота не должен превышать 5 МБ.");
                }

                return new(
                    null,
                    await MobileApiErrorReader.ReadAsync(
                        response,
                        cancellationToken));
            }

            var ticket = await response.Content.ReadFromJsonAsync<
                SupportTicketResponse>(cancellationToken);
            return ticket is null
                ? ApiResult<SupportTicketResponse>.Failure(
                    "Сервер не подтвердил отправку обращения.")
                : ApiResult<SupportTicketResponse>.Success(ticket);
        }
        catch (HttpRequestException)
        {
            return ApiResult<SupportTicketResponse>.Failure(
                "Нет соединения с сервером. Проверьте интернет и повторите отправку.");
        }
    }

    private static void AddOptional(
        MultipartFormDataContent content,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            content.Add(new StringContent(value), name);
    }

    private static string GetSafeUploadFileName(string contentType) =>
        contentType switch
        {
            "image/png" => "screenshot.png",
            "image/webp" => "screenshot.webp",
            _ => "screenshot.jpg"
        };
}
