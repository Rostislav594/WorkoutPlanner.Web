using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services.Admin;

/// <summary>
/// Считает непросмотренное в админке. Сервис scoped, то есть один на вкладку
/// админки (Blazor-цепь): страница раздела отмечает просмотр, а меню в той же
/// вкладке узнаёт об этом через <see cref="Changed"/> и сразу гасит подсветку.
///
/// Роль админа проверяется по данным входа, а не через UserManager: меню
/// и главная страница опрашивают сервис одновременно, а UserManager работает на общем
/// для вкладки контексте базы и не выдерживает параллельных запросов.
/// </summary>
public sealed class AdminAttentionService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    AuthenticationStateProvider authenticationStateProvider,
    TimeProvider timeProvider) : IAdminAttentionService
{
    private static readonly AdminSection[] Sections = Enum.GetValues<AdminSection>();

    public event Action? Changed;

    public async Task<AdminAttentionSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var adminUserId = await GetAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var viewedAt = await GetViewedAtAsync(db, adminUserId, cancellationToken);

        var usersSince = viewedAt[AdminSection.Users];
        var newUsers = await db.UserActivities.CountAsync(
            x => x.RegisteredAtUtc != null && x.RegisteredAtUtc > usersSince,
            cancellationToken);

        // Обращение непросмотрено, если оно создано или пользователь ответил в нём
        // после последнего захода в раздел поддержки.
        var supportSince = viewedAt[AdminSection.Support];
        var unseenTickets = await db.SupportTickets.CountAsync(
            x => x.CreatedAtUtc > supportSince ||
                 x.Messages.Any(message =>
                     message.SenderType == SupportMessageSenderType.User &&
                     message.CreatedAtUtc > supportSince),
            cancellationToken);

        return new AdminAttentionSnapshot(newUsers, unseenTickets);
    }

    public async Task<DateTime?> MarkViewedAsync(
        AdminSection section,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = await GetAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var view = await db.AdminSectionViews.FirstOrDefaultAsync(
            x => x.AdminUserId == adminUserId && x.Section == section,
            cancellationToken);
        var previous = view?.LastViewedAtUtc;
        if (view is null)
        {
            db.AdminSectionViews.Add(new AdminSectionView
            {
                AdminUserId = adminUserId,
                Section = section,
                LastViewedAtUtc = now
            });
        }
        else
        {
            view.LastViewedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        Changed?.Invoke();
        return previous;
    }

    private async Task<string> GetAdminUserIdAsync()
    {
        var user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || !user.IsInRole(ApplicationRoles.Admin))
            throw new UnauthorizedAccessException("Administrator role is required.");

        return userId;
    }

    /// <summary>
    /// Время последнего просмотра каждого раздела. При самом первом обращении
    /// точкой отсчёта становится текущий момент — старые записи не считаются новыми.
    /// </summary>
    private async Task<Dictionary<AdminSection, DateTime>> GetViewedAtAsync(
        WorkoutDbContext db,
        string adminUserId,
        CancellationToken cancellationToken)
    {
        var viewedAt = await db.AdminSectionViews
            .AsNoTracking()
            .Where(x => x.AdminUserId == adminUserId)
            .ToDictionaryAsync(x => x.Section, x => x.LastViewedAtUtc, cancellationToken);

        var missing = Sections.Where(section => !viewedAt.ContainsKey(section)).ToArray();
        if (missing.Length == 0)
            return viewedAt;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var section in missing)
        {
            db.AdminSectionViews.Add(new AdminSectionView
            {
                AdminUserId = adminUserId,
                Section = section,
                LastViewedAtUtc = now
            });
            viewedAt[section] = now;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Меню и главная страница могли создать точку отсчёта одновременно.
            // Тогда берём ту, что уже сохранена.
            db.ChangeTracker.Clear();
            var saved = await db.AdminSectionViews
                .AsNoTracking()
                .Where(x => x.AdminUserId == adminUserId)
                .ToDictionaryAsync(x => x.Section, x => x.LastViewedAtUtc, cancellationToken);
            foreach (var section in Sections)
                saved.TryAdd(section, now);
            return saved;
        }

        return viewedAt;
    }
}
