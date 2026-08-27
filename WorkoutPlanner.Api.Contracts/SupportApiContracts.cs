namespace WorkoutPlanner.Api.Contracts;

public sealed record SupportTicketResponse(
    string TicketNumber,
    DateTime CreatedAtUtc);

public sealed record InboxMessageResponse(
    long Id,
    string Type,
    string Title,
    string? Preview,
    string Body,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    string? SupportTicketNumber,
    bool IsPublication = false,
    string? ImagePath = null);

public sealed record InboxMessagesResponse(
    IReadOnlyList<InboxMessageResponse> Messages,
    int UnreadCount);

public sealed record InboxUnreadCountResponse(int UnreadCount);
