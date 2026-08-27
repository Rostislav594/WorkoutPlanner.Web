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
    public async Task<AdminDashboardSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await access.GetRequiredAdminUserIdAsync();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hours24 = now.AddHours(-24);
        var days7 = now.AddDays(-7);
        var days30 = now.AddDays(-30);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var activities = db.Set<UserActivity>().AsNoTracking();

        var totalUsers = await db.Users.LongCountAsync(cancellationToken);
        var new24 = await activities.LongCountAsync(
            x => x.RegisteredAtUtc != null && x.RegisteredAtUtc >= hours24,
            cancellationToken);
        var new7 = await activities.LongCountAsync(
            x => x.RegisteredAtUtc != null && x.RegisteredAtUtc >= days7,
            cancellationToken);
        var new30 = await activities.LongCountAsync(
            x => x.RegisteredAtUtc != null && x.RegisteredAtUtc >= days30,
            cancellationToken);
        var active24 = await activities.LongCountAsync(
            x => x.LastSeenAtUtc != null && x.LastSeenAtUtc >= hours24,
            cancellationToken);
        var active7 = await activities.LongCountAsync(
            x => x.LastSeenAtUtc != null && x.LastSeenAtUtc >= days7,
            cancellationToken);
        var active30 = await activities.LongCountAsync(
            x => x.LastSeenAtUtc != null && x.LastSeenAtUtc >= days30,
            cancellationToken);
        var inactive30 = await activities.LongCountAsync(
            x => x.LastSeenAtUtc < days30 ||
                 (x.LastSeenAtUtc == null &&
                  x.RegisteredAtUtc != null &&
                  x.RegisteredAtUtc < days30),
            cancellationToken);

        var workout24 = await db.WorkoutHistory.LongCountAsync(
            x => x.Date >= hours24,
            cancellationToken);
        var workout7 = await db.WorkoutHistory.LongCountAsync(
            x => x.Date >= days7,
            cancellationToken);
        var workout30 = await db.WorkoutHistory.LongCountAsync(
            x => x.Date >= days30,
            cancellationToken);
        var openTickets = await db.SupportTickets.LongCountAsync(
            x => x.Status != SupportTicketStatus.Resolved &&
                 x.Status != SupportTicketStatus.Closed,
            cancellationToken);
        var newTickets = await db.SupportTickets.LongCountAsync(
            x => x.Status == SupportTicketStatus.New,
            cancellationToken);

        return new AdminDashboardSnapshot(
            totalUsers,
            new24,
            new7,
            new30,
            active24,
            active7,
            active30,
            inactive30,
            workout24,
            workout7,
            workout30,
            openTickets,
            newTickets,
            BackendErrors24Hours: null);
    }
}
