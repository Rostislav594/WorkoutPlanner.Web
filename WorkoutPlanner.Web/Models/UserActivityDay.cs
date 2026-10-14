using System.ComponentModel.DataAnnotations;

namespace WorkoutPlanner.Web.Models;

/// <summary>
/// Пользователь был активен в этот день (по местному времени сервера).
/// Одна строка на пользователя в день — по ним админка рисует график активности.
/// </summary>
public sealed class UserActivityDay
{
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public DateOnly Day { get; set; }
}
