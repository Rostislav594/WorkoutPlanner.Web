using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Services.Push;

public sealed class DisabledRemotePushProvider : IRemotePushProvider
{
    public Task<PushDeliveryResult> SendAsync(
        PushDeliveryMessage message,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(PushDeliveryResult.Disabled);
}
