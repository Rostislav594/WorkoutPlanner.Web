using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.Admin;

public sealed class AdminDashboardService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    AdminAccessVerifier access,
    TimeProvider timeProvider) : IAdminDashboardService
{
    private const int ChartDays = 30;

    public async Task<AdminDashboardSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await access.GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var totalUsers = await db.Users.LongCountAsync(cancellationToken);
        var openTickets = await db.SupportTickets.LongCountAsync(
            x => x.Status != SupportTicketStatus.Resolved &&
                 x.Status != SupportTicketStatus.Closed,
            cancellationToken);
        var newTickets = await db.SupportTickets.LongCountAsync(
            SupportTicketQueries.IsNewForAdmin,
            cancellationToken);

        // Дни считаем по местному времени: «сегодня» должно совпадать с тем, что видит админ.
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var firstDay = today.AddDays(-(ChartDays - 1));
        var localZone = timeProvider.LocalTimeZone;

        var firstDayStartUtc = TimeZoneInfo.ConvertTimeToUtc(firstDay.ToDateTime(TimeOnly.MinValue), localZone);
        var registeredAt = await db.UserActivities.AsNoTracking()
            .Where(x => x.RegisteredAtUtc != null && x.RegisteredAtUtc >= firstDayStartUtc)
            .Select(x => x.RegisteredAtUtc!.Value)
            .ToListAsync(cancellationToken);
        var registrations = CountByDay(
            registeredAt.Select(utc => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), localZone))),
            firstDay);

        // Каждая строка — один пользователь в один день, поэтому счёт строк и есть число активных.
        var activeDays = await db.UserActivityDays.AsNoTracking()
            .Where(x => x.Day >= firstDay)
            .GroupBy(x => x.Day)
            .Select(group => new { Day = group.Key, Users = group.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Users, cancellationToken);
        var activeUsers = Enumerable.Range(0, ChartDays)
            .Select(offset => firstDay.AddDays(offset))
            .Select(day => new AdminDailyCount(day, activeDays.GetValueOrDefault(day)))
            .ToArray();

        return new AdminDashboardSnapshot(
            totalUsers,
            openTickets,
            newTickets,
            registrations,
            activeUsers);
    }

    private static AdminDailyCount[] CountByDay(IEnumerable<DateOnly> days, DateOnly firstDay)
    {
        var counts = days
            .Where(day => day >= firstDay)
            .GroupBy(day => day)
            .ToDictionary(group => group.Key, group => group.Count());
        return Enumerable.Range(0, ChartDays)
            .Select(offset => firstDay.AddDays(offset))
            .Select(day => new AdminDailyCount(day, counts.GetValueOrDefault(day)))
            .ToArray();
    }
}
