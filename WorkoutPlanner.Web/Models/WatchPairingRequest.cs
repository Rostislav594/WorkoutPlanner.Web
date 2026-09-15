namespace WorkoutPlanner.Web.Models;

/// <summary>
/// Заявка часов на сопряжение, которую пользователь подтверждает на телефоне.
///
/// Отличие от <see cref="WatchPairingCode"/>: код создаёт уже вошедший пользователь,
/// а заявку создают часы ещё до того, как известно, кому они принадлежат.
/// Поэтому <see cref="ApprovedByUserId"/> заполняется только в момент подтверждения.
///
/// <see cref="RequestId"/> — публичный идентификатор, он уезжает на телефон внутри
/// ссылки. <see cref="PollTokenHash"/> — отдельный секрет, известный только часам:
/// знание одного лишь RequestId не даёт забрать токены.
/// </summary>
public sealed class WatchPairingRequest
{
    public Guid Id { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string PollTokenHash { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public string? AppVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }

    /// <summary>Момент выдачи токенов часам. Делает заявку одноразовой.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    public int PollCount { get; set; }
}
