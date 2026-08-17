using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace GymPlanner.Mobile.Infrastructure;

public sealed record MobileApiOptions(Uri BaseAddress)
{
    private const string ConfigurationKey = "MobileApi:BaseAddress";
    private const string EnvironmentVariableName = "GYMPLANNER_API_BASE_ADDRESS";
    private const string AssemblyMetadataKey = "GymPlannerApiBaseAddress";

    public static MobileApiOptions CreateDefault(IConfiguration? configuration = null)
    {
        var configuredAddress = configuration?[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(configuredAddress))
            configuredAddress = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(configuredAddress))
        {
            configuredAddress = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute =>
                    string.Equals(
                        attribute.Key,
                        AssemblyMetadataKey,
                        StringComparison.Ordinal))
                ?.Value;
        }

        if (!string.IsNullOrWhiteSpace(configuredAddress))
            return new(CreateValidatedBaseAddress(configuredAddress));

        #if ANDROID
                return new(new Uri("https://10.0.2.2:7196/", UriKind.Absolute));
        #else
                return new(new Uri("https://localhost:7196/", UriKind.Absolute));
        #endif
    }

    private static Uri CreateValidatedBaseAddress(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var address) ||
            !IsAllowedSchemeAndHost(address) ||
            !string.IsNullOrEmpty(address.UserInfo) ||
            !string.IsNullOrEmpty(address.Query) ||
            !string.IsNullOrEmpty(address.Fragment))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} must be an absolute HTTPS URI without credentials, query, or fragment. " +
                "Debug builds also allow HTTP for localhost and private or link-local IP addresses.");
        }

        return address.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            ? address
            : new UriBuilder(address) { Path = $"{address.AbsolutePath}/" }.Uri;
    }

    private static bool IsAllowedSchemeAndHost(Uri address)
    {
        if (string.Equals(
                address.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

#if DEBUG
        return string.Equals(
                address.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) &&
            IsLocalDevelopmentHost(address.Host);
#else
        return false;
#endif
    }

#if DEBUG
    private static bool IsLocalDevelopmentHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var address))
            return false;

        if (IPAddress.IsLoopback(address))
            return true;

        var bytes = address.GetAddressBytes();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork =>
                bytes[0] == 10 ||
                bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                bytes[0] == 192 && bytes[1] == 168 ||
                bytes[0] == 169 && bytes[1] == 254,
            AddressFamily.InterNetworkV6 =>
                (bytes[0] & 0xfe) == 0xfc ||
                bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80,
            _ => false
        };
    }
#endif
}
