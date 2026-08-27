using System.ComponentModel.DataAnnotations;

namespace WorkoutPlanner.Web.Models;

public sealed class InboxPublication
{
    public long Id { get; set; }
    public InboxMessageType Type { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = string.Empty;
    [MaxLength(400)] public string? Preview { get; set; }
    [Required, MaxLength(8000)] public string Body { get; set; } = string.Empty;
    [MaxLength(300)] public string? ImagePath { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }
    public bool SendPushNotification { get; set; }
    public ICollection<InboxPublicationRead> Reads { get; set; } = [];
}

public sealed class InboxPublicationRead
{
    public long PublicationId { get; set; }
    public InboxPublication Publication { get; set; } = null!;
    [Required] public string UserId { get; set; } = string.Empty;
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
