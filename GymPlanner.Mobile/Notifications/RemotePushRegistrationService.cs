using System.Net.Http.Json;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Notifications;

public sealed class RemotePushRegistrationService(HttpClient client) : IRemotePushRegistrationService
{
    public async Task RegisterAsync(RegisterPushDeviceRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await client.PostAsJsonAsync("api/v1/push/devices", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UnregisterAsync(string installationId, CancellationToken cancellationToken = default)
    {
        using var response = await client.DeleteAsync($"api/v1/push/devices/{Uri.EscapeDataString(installationId)}", cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }
}
