using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IPushDeviceRegistrationService
{
    Task<PushDeviceRegistrationResult> RegisterAsync(
        string userId,
        RegisterPushDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UnregisterAsync(
        string userId,
        string installationId,
        CancellationToken cancellationToken = default);
}

public interface IPushDeviceStore
{
    Task<IReadOnlyList<PushDeviceTarget>> GetActiveTargetsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(string pushToken, CancellationToken cancellationToken = default);
}
