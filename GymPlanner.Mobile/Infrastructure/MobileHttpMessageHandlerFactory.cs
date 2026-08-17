using System.Net.Security;

namespace GymPlanner.Mobile.Infrastructure;

internal static class MobileHttpMessageHandlerFactory
{
    public static HttpClientHandler Create()
    {
        var handler = new HttpClientHandler();

#if DEBUG && ANDROID
        handler.ServerCertificateCustomValidationCallback =
            static (request, certificate, _, errors) =>
                errors == SslPolicyErrors.None ||
                IsAndroidEmulatorDevelopmentCertificate(request, certificate);
#endif

        return handler;
    }

#if DEBUG && ANDROID
    private static bool IsAndroidEmulatorDevelopmentCertificate(
        HttpRequestMessage request,
        System.Security.Cryptography.X509Certificates.X509Certificate2? certificate) =>
        request.RequestUri is { } requestUri &&
        string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(requestUri.Host, "10.0.2.2", StringComparison.Ordinal) &&
        certificate is not null &&
        string.Equals(certificate.Subject, "CN=localhost", StringComparison.Ordinal) &&
        string.Equals(certificate.Issuer, "CN=localhost", StringComparison.Ordinal);
#endif
}
