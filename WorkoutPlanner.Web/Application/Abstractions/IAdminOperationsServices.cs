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

    Task MarkViewedAsync(AdminSection section, CancellationToken cancellationToken = default);
}
