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
}
