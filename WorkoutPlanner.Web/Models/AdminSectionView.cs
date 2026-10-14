using System.ComponentModel.DataAnnotations;

namespace WorkoutPlanner.Web.Models;

/// <summary>
/// Когда админ последний раз открывал раздел админки. Всё, что появилось
/// в разделе позже, считается непросмотренным и подсвечивается.
/// </summary>
public sealed class AdminSectionView
{
    [Required]
    [MaxLength(450)]
    public string AdminUserId { get; set; } = string.Empty;

    public AdminSection Section { get; set; }

    public DateTime LastViewedAtUtc { get; set; }
}

public enum AdminSection
{
    Users,
    Support
}
