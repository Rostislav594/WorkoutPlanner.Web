using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.Admin;

public sealed class AdminAuditService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    AdminAccessVerifier access) : IAdminAuditService
{
    public async Task<AdminPagedResult<AdminAuditItem>> GetPageAsync(
        int page,
        int pageSize,
        string? targetType = null,
        CancellationToken cancellationToken = default)
    {
        await access.GetRequiredAdminUserIdAsync();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var logs = db.AdminAuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(targetType))
            logs = logs.Where(x => x.TargetType == targetType);
        var total = await logs.LongCountAsync(cancellationToken);
        var rows = await logs
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.CreatedAtUtc,
                x.Action,
                x.TargetType,
                x.TargetId,
                AdminEmail = db.Users.Where(user => user.Id == x.AdminUserId)
                    .Select(user => user.Email ?? user.UserName)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
            return new AdminPagedResult<AdminAuditItem>([], page, pageSize, total);

        // Подписи объектов одним запросом на тип: номер обращения, почта, заголовок публикации.
        var ticketIds = NumericIds(rows.Where(x => x.TargetType == nameof(SupportTicket)).Select(x => x.TargetId));
        var tickets = await db.SupportTickets.AsNoTracking()
            .Where(x => ticketIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.TicketNumber ?? x.Id.ToString(), cancellationToken);
        var publicationIds = NumericIds(rows.Where(x => x.TargetType == nameof(InboxPublication)).Select(x => x.TargetId));
        var publications = await db.InboxPublications.AsNoTracking()
            .Where(x => publicationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);
        var userIds = rows.Where(x => x.TargetType == AdminAuditTargets.User).Select(x => x.TargetId).Distinct().ToArray();
        var users = await db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email ?? x.UserName ?? x.Id, cancellationToken);

        var items = rows.Select(row =>
        {
            var (label, url) = row.TargetType switch
            {
                nameof(SupportTicket) when TryParseId(row.TargetId, out var id) && tickets.TryGetValue(id, out var number) =>
                    (number, $"/admin/support/{id}"),
                nameof(InboxPublication) when TryParseId(row.TargetId, out var id) && publications.TryGetValue(id, out var title) =>
                    (title, "/admin/publications"),
                AdminAuditTargets.User when users.TryGetValue(row.TargetId, out var email) =>
                    (email, $"/admin/users/{Uri.EscapeDataString(row.TargetId)}"),
                // Объект удалён: показываем только тип и номер.
                _ => ((string?)null, (string?)null)
            };
            return new AdminAuditItem(
                row.Id,
                row.CreatedAtUtc,
                row.AdminEmail ?? string.Empty,
                row.Action,
                row.TargetType,
                row.TargetId,
                label,
                url);
        }).ToArray();
        return new AdminPagedResult<AdminAuditItem>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyDictionary<string, long>> CountByTargetTypeAsync(CancellationToken cancellationToken = default)
    {
        await access.GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AdminAuditLogs.AsNoTracking()
            .GroupBy(x => x.TargetType)
            .Select(group => new { group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }

    private static long[] NumericIds(IEnumerable<string> ids) =>
        ids.Select(id => TryParseId(id, out var value) ? value : (long?)null)
            .OfType<long>()
            .Distinct()
            .ToArray();

    private static bool TryParseId(string value, out long id) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id);
}
