using System.ComponentModel.DataAnnotations;

namespace WorkoutPlanner.Web.Models;

public sealed class InboxMessage
{
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public long? SupportTicketId { get; set; }

    public SupportTicket? SupportTicket { get; set; }

    public InboxMessageType Type { get; set; }

    [Required]
    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Preview { get; set; }

    [Required]
    [MaxLength(8000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public long? TelegramMessageId { get; set; }
}

public enum InboxMessageType
{
    SupportReply,
    News,
    Update,
    System
}
