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

    private static void AddActivityMetadata(HttpRequestMessage request)
    {
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
