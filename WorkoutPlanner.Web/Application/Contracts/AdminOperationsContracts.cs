using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Application.Contracts;

public enum AdminUserActivityStatus
{
    Active,
    Dormant,
    Inactive,
    NeverActive
}

public sealed record AdminDashboardSnapshot(
    long TotalUsers,
    long OpenSupportTickets,
    long NewSupportTickets,
    IReadOnlyList<AdminDailyCount> Registrations,
    IReadOnlyList<AdminDailyCount> ActiveUsers);

/// <summary>Значение за один день (по местному времени сервера).</summary>
public sealed record AdminDailyCount(DateOnly Date, int Count);

public sealed record AdminPagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record AdminUserListQuery(string? Search, int Page = 1, int PageSize = 25);

public sealed record AdminUserListItem(
    string UserId,
    string DisplayName,
    string Email,
    DateTime? RegisteredAtUtc,
    DateTime? LastSeenAtUtc,
    AdminUserActivityStatus ActivityStatus,
    string? Platform,
    string? AppVersion,
    long WorkoutCount,
    long SupportTicketCount,
    bool IsBlocked = false);

public sealed record AdminUserDetail(
    string UserId,
    string DisplayName,
    string Email,
    DateTime? RegisteredAtUtc,
    DateTime? LastSeenAtUtc,
    AdminUserActivityStatus ActivityStatus,
    string? Platform,
    string? AppVersion,
    string? OsVersion,
    string? DeviceModel,
    long WorkoutCount,
    long SupportTicketCount,
    long ActiveMobileSessionCount,
    DateTimeOffset? LockoutEnd,
    bool IsBlocked,
    bool IsAdmin,
    IReadOnlyList<AdminUserTicketItem> RecentTickets);

/// <summary>Обращение пользователя в его карточке.</summary>
public sealed record AdminUserTicketItem(
    long Id,
    string TicketNumber,
    string Preview,
    DateTime UpdatedAtUtc,
    SupportTicketStatus Status);

/// <summary>
/// Личное сообщение пользователю: приходит во «Входящие» и push-уведомлением.
/// Отключить push может только сам пользователь в настройках приложения.
/// </summary>
public sealed record AdminUserMessage(string Title, string Body);

public sealed record AdminSupportListQuery(
    string? Search,
    SupportTicketStatus? Status,
    int Page = 1,
    int PageSize = 25,
    SupportTicketKind Kind = SupportTicketKind.Problem);

public sealed record AdminSupportListItem(
    long Id,
    string TicketNumber,
    string UserId,
    string DisplayName,
    string Email,
    string Preview,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    SupportTicketStatus Status,
    bool HasScreenshot,
    bool IsNew = false);

public sealed record AdminSupportMessageItem(
    long Id,
    SupportMessageSenderType SenderType,
    string Message,
    DateTime CreatedAtUtc);

public sealed record AdminSupportTicketDetail(
    long Id,
    string TicketNumber,
    string UserId,
    string DisplayName,
    string Email,
    string Message,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    SupportTicketStatus Status,
    string? AppVersion,
    string? Platform,
    string? OsVersion,
    string? DeviceModel,
    bool HasScreenshot,
    IReadOnlyList<AdminSupportMessageItem> Messages,
    SupportTicketKind Kind = SupportTicketKind.Problem);

public sealed record AdminSupportReply(string Message, SupportTicketStatus? Status = null);

/// <summary>Сколько нового админ ещё не видел в разделах админки.</summary>
public sealed record AdminAttentionSnapshot(
    int NewUsers,
    int SupportTicketsAwaitingReply,
    int IdeasAwaitingReply = 0);

/// <summary>Запись журнала действий админки с понятной подписью объекта.</summary>
public sealed record AdminAuditItem(
    long Id,
    DateTime CreatedAtUtc,
    string AdminEmail,
    string Action,
    string TargetType,
    string TargetId,
    string? TargetLabel,
    string? TargetUrl);
