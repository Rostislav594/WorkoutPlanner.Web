using System.ComponentModel.DataAnnotations;

namespace WorkoutPlanner.Web.Models;

public sealed class SupportMessage
{
    public long Id { get; set; }

    public long SupportTicketId { get; set; }

    public SupportTicket SupportTicket { get; set; } = null!;

    public SupportMessageSenderType SenderType { get; set; }

    [Required]
    [MaxLength(8000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

public enum SupportMessageSenderType
{
    User,
    Support
}
