using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.Activity;

public sealed class UserActivityService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    IOptions<UserActivityOptions> options,
    TimeProvider timeProvider)
{
    public async Task EnsureRegisteredAsync(
        string userId,
        DateTime? registeredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.UserActivities.AnyAsync(x => x.UserId == userId, cancellationToken))
            return;

        db.UserActivities.Add(new UserActivity
        {
            UserId = userId,
            RegisteredAtUtc = registeredAtUtc
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (!await ActivityExistsAsync(userId, cancellationToken))
                throw;
            // A concurrent registration/login request created the row first.
        }
    }

    public async Task RecordSeenAsync(
        string userId,
        UserActivityMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(metadata);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var throttle = options.Value.WriteThrottle;
        if (throttle < TimeSpan.Zero)
            throttle = TimeSpan.Zero;
        var writeBefore = now - throttle;
        // Первый запрос нового дня пишется всегда, даже внутри паузы между записями:
        // так у каждого активного дня есть отметка для графика активности.
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            today.ToDateTime(TimeOnly.MinValue),
            timeProvider.LocalTimeZone);
        var platform = Normalize(metadata.Platform, 40);
        var appVersion = Normalize(metadata.AppVersion, 40);
        var osVersion = Normalize(metadata.OsVersion, 80);
        var deviceModel = Normalize(metadata.DeviceModel, 120);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var updated = await db.UserActivities
            .Where(x => x.UserId == userId &&
                        (x.LastSeenAtUtc == null || x.LastSeenAtUtc <= writeBefore || x.LastSeenAtUtc < todayStartUtc))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.LastSeenAtUtc, now)
                    .SetProperty(x => x.Platform, x => platform ?? x.Platform)
                    .SetProperty(x => x.AppVersion, x => appVersion ?? x.AppVersion)
                    .SetProperty(x => x.OsVersion, x => osVersion ?? x.OsVersion)
                    .SetProperty(x => x.DeviceModel, x => deviceModel ?? x.DeviceModel),
                cancellationToken);
        if (updated > 0)
        {
            await RecordActiveDayAsync(userId, today, cancellationToken);
            return;
        }

        if (await db.UserActivities.AnyAsync(x => x.UserId == userId, cancellationToken))
            return;

        db.UserActivities.Add(new UserActivity
        {
            UserId = userId,
            RegisteredAtUtc = null,
            LastSeenAtUtc = now,
            Platform = platform,
            AppVersion = appVersion,
            OsVersion = osVersion,
            DeviceModel = deviceModel
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (!await ActivityExistsAsync(userId, cancellationToken))
                throw;
            await db.UserActivities
                .Where(x => x.UserId == userId &&
                            (x.LastSeenAtUtc == null || x.LastSeenAtUtc <= writeBefore || x.LastSeenAtUtc < todayStartUtc))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.LastSeenAtUtc, now)
                        .SetProperty(x => x.Platform, x => platform ?? x.Platform)
                        .SetProperty(x => x.AppVersion, x => appVersion ?? x.AppVersion)
                        .SetProperty(x => x.OsVersion, x => osVersion ?? x.OsVersion)
                        .SetProperty(x => x.DeviceModel, x => deviceModel ?? x.DeviceModel),
                    cancellationToken);
        }

        await RecordActiveDayAsync(userId, today, cancellationToken);
    }

    /// <summary>Отмечает, что пользователь был активен сегодня; повторная отметка ничего не меняет.</summary>
    private async Task RecordActiveDayAsync(
        string userId,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.UserActivityDays.AnyAsync(x => x.Day == day && x.UserId == userId, cancellationToken))
            return;

        db.UserActivityDays.Add(new UserActivityDay { UserId = userId, Day = day });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Параллельный запрос мог отметить день первым — тогда всё в порядке.
            await using var verificationDb = await dbFactory.CreateDbContextAsync(cancellationToken);
            if (!await verificationDb.UserActivityDays.AsNoTracking()
                    .AnyAsync(x => x.Day == day && x.UserId == userId, cancellationToken))
                throw;
        }
    }

    private async Task<bool> ActivityExistsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await using var verificationDb = await dbFactory.CreateDbContextAsync(
            cancellationToken);
        return await verificationDb.UserActivities.AsNoTracking()
            .AnyAsync(x => x.UserId == userId, cancellationToken);
    }

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
