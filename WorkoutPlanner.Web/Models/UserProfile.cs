using System.ComponentModel.DataAnnotations;

namespace WorkoutPlanner.Web.Models;

public class UserProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    public DateTime? BirthDate { get; set; }

    [Required]
    [MaxLength(30)]
    public string Gender { get; set; } = string.Empty;

    public int RestBetweenSetsSeconds { get; set; } = 90;

    public int RestBetweenExercisesSeconds { get; set; } = 120;

    /// <summary>
    /// Язык интерфейса: "ru", "uk" или "en" (см. AppLanguages).
    /// Хранится в профиле, а не на устройстве, потому что push-уведомления
    /// формируются на сервере и должны приходить на языке пользователя.
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "ru";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
