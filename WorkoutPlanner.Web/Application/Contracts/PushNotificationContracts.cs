using WorkoutPlanner.Api.Contracts;

namespace WorkoutPlanner.Web.Application.Contracts;

public sealed record PushNotificationTemplate(string Title, string Body);

public sealed record PushDeliveryMessage(
    string UserId,
    PushNotificationType Type,
    long InboxMessageId,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);

public enum PushDeliveryStatus
{
    Sent,
    NoRecipients,
    Disabled,
    Failed
}

public sealed record PushDeliveryResult(PushDeliveryStatus Status)
{
    public static PushDeliveryResult Sent { get; } = new(PushDeliveryStatus.Sent);
    public static PushDeliveryResult NoRecipients { get; } = new(PushDeliveryStatus.NoRecipients);
    public static PushDeliveryResult Disabled { get; } = new(PushDeliveryStatus.Disabled);
    public static PushDeliveryResult Failed { get; } = new(PushDeliveryStatus.Failed);
}
