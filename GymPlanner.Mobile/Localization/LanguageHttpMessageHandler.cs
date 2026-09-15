using System.Net.Http.Headers;

namespace GymPlanner.Mobile.Localization;

/// <summary>
/// Добавляет к каждому запросу Accept-Language с текущим языком приложения,
/// чтобы сервер отдавал сообщения и push-уведомления на нужном языке.
/// </summary>
public sealed class LanguageHttpMessageHandler(IAppLanguageService language)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.Add(
            new StringWithQualityHeaderValue(language.Current));

        return base.SendAsync(request, cancellationToken);
    }
}
