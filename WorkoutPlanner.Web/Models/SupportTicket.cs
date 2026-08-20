using System.ComponentModel.DataAnnotations;

namespace WorkoutPlanner.Web.Models;

public sealed class SupportTicket
{
    public long Id { get; set; }

    [MaxLength(32)]
    public string? TicketNumber { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.New;

    [MaxLength(160)]
    public string? ScreenshotPath { get; set; }

    [MaxLength(40)]
    public string? AppVersion { get; set; }

    [MaxLength(40)]
    public string? Platform { get; set; }

    [MaxLength(80)]
    public string? OsVersion { get; set; }

    [MaxLength(120)]
    public string? DeviceModel { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public long? TelegramMessageId { get; set; }

    public ICollection<InboxMessage> InboxMessages { get; } = [];

    public SupportDeliveryStatus TelegramDeliveryStatus { get; set; } =
        SupportDeliveryStatus.Pending;
}

public enum SupportTicketStatus
{
    New,
    InProgress,
    WaitingForUser,
    Resolved,
    Closed
}

public enum SupportDeliveryStatus
{
    Pending,
    Sent,
    Failed,
    Disabled
}
