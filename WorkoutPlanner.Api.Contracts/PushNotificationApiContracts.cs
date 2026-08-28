namespace WorkoutPlanner.Api.Contracts;

public sealed record RegisterPushDeviceRequest(
    string InstallationId,
    string Platform,
    string PushToken);

public sealed record PushDeviceRegistrationResponse(
    string InstallationId,
    string Platform,
    DateTimeOffset UpdatedAt);

public enum PushNotificationType
{
    SupportReply,
    InboxMessage,
    AppUpdate,
    AccountNotification
}

public static class PushNotificationPayloadKeys
{
    public const string Type = "type";
    public const string InboxMessageId = "inboxMessageId";
}

public static class PushNotificationTypeSerializer
{
    public static string ToPayloadValue(PushNotificationType type) => type switch
    {
        PushNotificationType.SupportReply => "support_reply",
        PushNotificationType.InboxMessage => "inbox_message",
        PushNotificationType.AppUpdate => "app_update",
        PushNotificationType.AccountNotification => "account_notification",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static bool TryParsePayloadValue(
        string? value,
        out PushNotificationType type)
    {
        type = value switch
        {
            "support_reply" => PushNotificationType.SupportReply,
            "inbox_message" => PushNotificationType.InboxMessage,
            "app_update" => PushNotificationType.AppUpdate,
            "account_notification" => PushNotificationType.AccountNotification,
            _ => default
        };

        return value is "support_reply" or "inbox_message" or
            "app_update" or "account_notification";
    }
}
