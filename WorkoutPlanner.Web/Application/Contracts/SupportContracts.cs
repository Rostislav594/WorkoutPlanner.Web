namespace WorkoutPlanner.Web.Application.Contracts;

public sealed record SupportTicketSubmission(
    string Message,
    PhotoUpload? Screenshot,
    string? AppVersion,
    string? Platform,
    string? OsVersion,
    string? DeviceModel);

public enum SupportTicketCreationFailure
{
    None,
    InvalidMessage,
    InvalidScreenshot
}

public sealed record SupportTicketCreationResult(
    bool Succeeded,
    string? TicketNumber,
    DateTime? CreatedAtUtc,
    SupportTicketCreationFailure Failure)
{
    public static SupportTicketCreationResult Success(
        string ticketNumber,
        DateTime createdAtUtc) =>
        new(true, ticketNumber, createdAtUtc, SupportTicketCreationFailure.None);

    public static SupportTicketCreationResult Invalid(
        SupportTicketCreationFailure failure) =>
        new(false, null, null, failure);
}

public sealed record SupportTicketNotification(
    long TicketId,
    string TicketNumber,
    string UserId,
    string Email,
    string DisplayName,
    string Message,
    string? ScreenshotPath,
    string? AppVersion,
    string? Platform,
    string? OsVersion,
    string? DeviceModel,
    DateTime CreatedAtUtc);

public sealed record SupportNotificationResult(
    bool Succeeded,
    bool Disabled,
    long? TelegramMessageId)
{
    public static SupportNotificationResult Sent(long? messageId) =>
        new(true, false, messageId);

    public static SupportNotificationResult Failed { get; } =
        new(false, false, null);

    public static SupportNotificationResult NotConfigured { get; } =
        new(false, true, null);
}
