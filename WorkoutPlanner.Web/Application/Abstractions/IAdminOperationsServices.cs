using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IAdminDashboardService
{
    Task<AdminDashboardSnapshot> GetAsync(CancellationToken cancellationToken = default);
}

public interface IAdminUserService
{
    Task<AdminPagedResult<AdminUserListItem>> GetPageAsync(
        AdminUserListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetail?> GetAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Блокирует вход и сразу выкидывает пользователя со всех устройств.
    /// Администратора заблокировать нельзя. Возвращает false, если пользователя нет.
    /// </summary>
    Task<bool> BlockAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> UnblockAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Завершает все сессии телефона и часов и отключает их push.</summary>
    Task<bool> SignOutEverywhereAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Кладёт личное сообщение во «Входящие». Возвращает его номер или null.</summary>
    Task<long?> SendMessageAsync(
        string userId,
        AdminUserMessage message,
        CancellationToken cancellationToken = default);
}

public interface IAdminSupportService
{
    Task<AdminPagedResult<AdminSupportListItem>> GetPageAsync(
        AdminSupportListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminSupportTicketDetail?> GetAsync(
        long ticketId,
        CancellationToken cancellationToken = default);

    Task<bool> ChangeStatusAsync(
        long ticketId,
        SupportTicketStatus status,
        CancellationToken cancellationToken = default);

    Task<bool> ReplyAsync(
        long ticketId,
        AdminSupportReply reply,
        CancellationToken cancellationToken = default);

    Task<PhotoDownload?> OpenScreenshotAsync(
        long ticketId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Непросмотренное в админке: новые пользователи и новые обращения или ответы
/// в них с момента, когда админ последний раз открывал раздел.
/// </summary>
public interface IAdminAttentionService
{
    /// <summary>Срабатывает, когда админ открыл раздел и его подсветка погасла.</summary>
    event Action? Changed;

    Task<AdminAttentionSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмечает раздел просмотренным. Возвращает время прошлого захода, чтобы
    /// страница могла пометить то, что появилось с тех пор.
    /// </summary>
    Task<DateTime?> MarkViewedAsync(AdminSection section, CancellationToken cancellationToken = default);

    /// <summary>Админ открыл обращение: оно перестаёт быть новым.</summary>
    Task MarkTicketViewedAsync(long ticketId, CancellationToken cancellationToken = default);
}

/// <summary>Журнал действий админки: что, кто и когда сделал.</summary>
public interface IAdminAuditService
{
    /// <param name="targetType">Раздел журнала (тип объекта); <c>null</c> — все записи.</param>
    Task<AdminPagedResult<AdminAuditItem>> GetPageAsync(
        int page,
        int pageSize,
        string? targetType = null,
        CancellationToken cancellationToken = default);

    /// <summary>Сколько записей в каждом разделе журнала (по типу объекта).</summary>
    Task<IReadOnlyDictionary<string, long>> CountByTargetTypeAsync(CancellationToken cancellationToken = default);
}
