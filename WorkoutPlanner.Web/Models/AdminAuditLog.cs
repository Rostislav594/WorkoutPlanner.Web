using System.ComponentModel.DataAnnotations;

namespace WorkoutPlanner.Web.Models;

public sealed class AdminAuditLog
{
    public long Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string AdminUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string TargetType { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string TargetId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
