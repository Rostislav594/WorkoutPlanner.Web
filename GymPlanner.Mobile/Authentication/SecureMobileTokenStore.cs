using System.Text.Json;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;

namespace GymPlanner.Mobile.Authentication;

public sealed class SecureMobileTokenStore(
    ILogger<SecureMobileTokenStore> logger) : IMobileTokenStore
{
    private const string StorageKey = "gymplanner.mobile.authentication";
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<MobileTokenSet?> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await SecureStorage.Default.GetAsync(StorageKey);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var tokens = JsonSerializer.Deserialize<MobileTokenSet>(
                json,
                SerializerOptions);
            return tokens is { AccessToken.Length: > 0, RefreshToken.Length: > 0 }
                ? tokens
                : null;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Stored mobile authentication state is invalid and will be cleared.");
            SecureStorage.Default.Remove(StorageKey);
            return null;
        }
    }

    public async Task WriteAsync(
        MobileTokenSet tokens,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(tokens, SerializerOptions);
        await SecureStorage.Default.SetAsync(StorageKey, json);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
