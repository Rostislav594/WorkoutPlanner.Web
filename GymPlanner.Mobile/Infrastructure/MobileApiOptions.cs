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
            !string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(address.UserInfo) ||
            !string.IsNullOrEmpty(address.Query) ||
            !string.IsNullOrEmpty(address.Fragment))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} must be an absolute HTTPS URI without credentials, query, or fragment.");
        }

        return address.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            ? address
            : new UriBuilder(address) { Path = $"{address.AbsolutePath}/" }.Uri;
    }
}
