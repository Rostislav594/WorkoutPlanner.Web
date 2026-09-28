using System.Net;

namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Внешний обработчик HTTP мобилки: ограничивает время запроса и сообщает,
/// отвечает ли сервер.
/// </summary>
/// <remarks>
/// Без собственного таймаута запрос в зале без связи висит стандартные 100 секунд,
/// а потом бросает исключение, которое клиенты API не ловят. Здесь истёкший таймаут
/// превращается в <see cref="HttpRequestException"/> — обычный «сервер недоступен».
/// </remarks>
public sealed class ReachabilityHttpHandler(ServerReachability reachability) : DelegatingHandler
{
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(90);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeoutFor(request));

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            MarkUnavailable();
            throw new HttpRequestException("The server did not respond in time.", exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkUnavailable();
            throw;
        }

        if ((int)response.StatusCode >= 500)
            TransportScope.Current?.MarkServerUnavailable();

        // 502–504 отдаёт прокси, когда самого сервера нет; остальное — ответ сервера.
        if (response.StatusCode is HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout)
        {
            reachability.ReportServerUnavailable();
        }
        else
        {
            reachability.ReportServerResponded();
        }

        return response;
    }

    private void MarkUnavailable()
    {
        TransportScope.Current?.MarkServerUnavailable();
        reachability.ReportServerUnavailable();
    }

    private static TimeSpan TimeoutFor(HttpRequestMessage request)
    {
        if (request.Content is MultipartFormDataContent)
            return UploadTimeout;

        return request.Method == HttpMethod.Get ? ReadTimeout : WriteTimeout;
    }
}
