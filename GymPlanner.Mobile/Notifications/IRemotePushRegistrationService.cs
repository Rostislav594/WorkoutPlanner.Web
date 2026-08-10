using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Notifications;

public interface IRemotePushRegistrationService
{
    Task RegisterAsync(
        RegisterPushDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task UnregisterAsync(
        string installationId,
        CancellationToken cancellationToken = default);
}
