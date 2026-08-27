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
    long NewUsers24Hours,
    long NewUsers7Days,
    long NewUsers30Days,
    long ActiveUsers24Hours,
    long ActiveUsers7Days,
    long ActiveUsers30Days,
    long InactiveUsersOver30Days,
    long CompletedWorkouts24Hours,
    long CompletedWorkouts7Days,
    long CompletedWorkouts30Days,
    long OpenSupportTickets,
    long NewSupportTickets,
    long? BackendErrors24Hours);

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
    long SupportTicketCount);

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
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    DateTimeOffset? LockoutEnd);

public sealed record AdminSupportListQuery(
    string? Search,
    SupportTicketStatus? Status,
    int Page = 1,
    int PageSize = 25);

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
    bool HasScreenshot);

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
    IReadOnlyList<AdminSupportMessageItem> Messages);

public sealed record AdminSupportReply(string Message, SupportTicketStatus? Status = null);

public enum AdminSystemHealthStatus
{
    Healthy,
    Warning,
    Error
}

public sealed record AdminSystemComponentHealth(
    string Component,
    AdminSystemHealthStatus Status,
    string Summary);

public sealed record AdminSystemHealthSnapshot(
    IReadOnlyList<AdminSystemComponentHealth> Components,
    DateTime CheckedAtUtc);
