namespace WorkoutPlanner.Web.Models;

/// <summary>
/// Запомненный ответ на изменяющий запрос телефона с заголовком <c>Idempotency-Key</c>.
/// </summary>
/// <remarks>
/// Офлайн-очередь повторяет запрос, если не дождалась ответа. Без этой записи
/// повтор завершения тренировки создал бы вторую запись в истории; с ней сервер
/// возвращает прежний ответ, не выполняя запрос заново.
/// </remarks>
public sealed class MobileIdempotencyRecord
{
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid Key { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    /// <summary><c>null</c>, пока первый запрос с этим ключом ещё выполняется.</summary>
    public int? StatusCode { get; set; }

    public string? ContentType { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
