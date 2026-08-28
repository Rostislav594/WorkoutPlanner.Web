using System.Globalization;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Services.Push;

public sealed class PushNotificationService(
    IPushNotificationTemplateProvider templates,
    IRemotePushProvider provider,
    ILogger<PushNotificationService> logger) : IPushNotificationService
{
    public async Task NotifyInboxMessageAsync(
        string userId,
        PushNotificationType type,
        long inboxMessageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (inboxMessageId <= 0)
            throw new ArgumentOutOfRangeException(nameof(inboxMessageId));

        var template = templates.Get(type);
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PushNotificationPayloadKeys.Type] =
                PushNotificationTypeSerializer.ToPayloadValue(type),
            [PushNotificationPayloadKeys.InboxMessageId] =
                inboxMessageId.ToString(CultureInfo.InvariantCulture)
        };

        PushDeliveryResult result;
        try
        {
            result = await provider.SendAsync(
                new PushDeliveryMessage(
                    userId,
                    type,
                    inboxMessageId,
                    template.Title,
                    template.Body,
                    data),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                          !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Push delivery failed for type {PushType}, inbox message {InboxMessageId}.",
                type,
                inboxMessageId);
            return;
        }

        if (result.Status == PushDeliveryStatus.Failed)
        {
            logger.LogWarning(
                "Push provider reported a delivery failure for type {PushType}, inbox message {InboxMessageId}.",
                type,
                inboxMessageId);
        }
    }
}
