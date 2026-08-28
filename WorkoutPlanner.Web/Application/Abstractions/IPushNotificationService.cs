using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IPushNotificationTemplateProvider
{
    PushNotificationTemplate Get(PushNotificationType type);
}

public interface IRemotePushProvider
{
    Task<PushDeliveryResult> SendAsync(
        PushDeliveryMessage message,
        CancellationToken cancellationToken = default);
}

public interface IPushNotificationService
{
    Task NotifyInboxMessageAsync(
        string userId,
        PushNotificationType type,
        long inboxMessageId,
        CancellationToken cancellationToken = default);
}
