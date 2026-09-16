using System.Net;
using System.Net.Http.Headers;

namespace GymPlanner.Mobile.Authentication;

public sealed class AuthenticatedHttpMessageHandler(
    MobileAuthenticationService authentication) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            AddActivityMetadata(request);

            var accessToken = await authentication.GetValidAccessTokenAsync(
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                await authentication.ClearAsync(cancellationToken);

            return response;
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException &&
            exception is not HttpRequestException)
        {
            // Транспорт на Android бросает не только HttpRequestException:
            // okhttp отдаёт собственные исключения вроде «unexpected end of
            // stream», когда сервер закрывает удерживаемое соединение — при
            // перезапуске сервера, обрыве мобильной сети или таймауте прокси.
            //
            // Клиенты API ловят HttpRequestException и возвращают ApiResult с
            // ошибкой, а всё остальное проходит их насквозь и всплывает в
            // OnInitializedAsync страницы. Там его уже никто не ловит, и
            // необработанное исключение убивает рендерер Blazor: интерфейс
            // замирает целиком — не работают ни кнопки, ни навигация, ни
            // закрытие открытого диалога.
            //
            // Поэтому любой сбой транспорта приводится здесь к типу, который
            // вышележащий код уже умеет обрабатывать. Отмена пробрасывается
            // как есть: это не ошибка.
            throw new HttpRequestException(exception.Message, exception);
        }
    }

    private static void AddActivityMetadata(HttpRequestMessage request)
    {
        AddHeader(request, "X-GPlanner-Installation-Id", Notifications.InstallationIdStore.Get());
        AddHeader(
            request,
            "X-GPlanner-Platform",
            Microsoft.Maui.Devices.DeviceInfo.Current.Platform.ToString());
        AddHeader(
            request,
            "X-GPlanner-App-Version",
            Microsoft.Maui.ApplicationModel.AppInfo.Current.VersionString);
        AddHeader(
            request,
            "X-GPlanner-OS-Version",
            Microsoft.Maui.Devices.DeviceInfo.Current.VersionString);
        AddHeader(
            request,
            "X-GPlanner-Device-Model",
            Microsoft.Maui.Devices.DeviceInfo.Current.Model);
    }

    private static void AddHeader(
        HttpRequestMessage request,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !request.Headers.Contains(name))
            request.Headers.TryAddWithoutValidation(name, value);
    }
}
