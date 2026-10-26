namespace WorkoutPlanner.Api.Contracts;

/// <summary>Значения поля <c>kind</c> при создании обращения.</summary>
public static class SupportTicketKinds
{
    public const string Problem = "problem";
    public const string Idea = "idea";
}

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
    string? ImagePath = null,
    bool IsArchived = false);

public sealed record InboxMessagesResponse(
    IReadOnlyList<InboxMessageResponse> Messages,
    int UnreadCount);

public sealed record InboxUnreadCountResponse(int UnreadCount);
