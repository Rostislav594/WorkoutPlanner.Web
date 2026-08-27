using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Application.Contracts;

public sealed record InboxMessageItem(
    long Id,
    InboxMessageType Type,
    string Title,
    string? Preview,
    string Body,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    string? SupportTicketNumber,
    bool IsPublication = false,
    string? ImagePath = null);

public sealed record InboxPage(
    IReadOnlyList<InboxMessageItem> Messages,
    int UnreadCount);

public sealed record TelegramSupportReply(
    long TelegramMessageId,
    long ReplyToTelegramMessageId,
    string Text,
    DateTime CreatedAtUtc);

/// <summary>
/// A reusable application-level request for future news, update, system and
/// push-notification publishers. It deliberately has no Telegram dependency.
/// </summary>
public sealed record InboxMessagePublication(
    string UserId,
    InboxMessageType Type,
    string Title,
    string Body,
    string? Preview = null,
    long? SupportTicketId = null);
