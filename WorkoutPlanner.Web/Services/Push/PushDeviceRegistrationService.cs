using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.Push;

public sealed class PushDeviceRegistrationService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    TimeProvider timeProvider) : IPushDeviceRegistrationService, IPushDeviceStore
{
    private static readonly HashSet<string> SupportedPlatforms = ["android", "ios"];

    public async Task<PushDeviceRegistrationResult> RegisterAsync(
        string userId,
        RegisterPushDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        Validate(request);
        var now = timeProvider.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // An installation identifies one physical app instance. If the user
        // changes accounts without a successful logout, do not leave the
        // previous account's registration active on that same device.
        await db.PushDeviceRegistrations
            .Where(x => x.InstallationId == request.InstallationId &&
                        x.UserId != userId &&
                        x.IsActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.UpdatedAtUtc, now.UtcDateTime),
                cancellationToken);

        var device = await db.PushDeviceRegistrations.SingleOrDefaultAsync(
            x => x.UserId == userId && x.InstallationId == request.InstallationId,
            cancellationToken);
        if (device is null)
        {
            device = new PushDeviceRegistration
            {
                Id = Guid.NewGuid(), UserId = userId, InstallationId = request.InstallationId,
                CreatedAtUtc = now.UtcDateTime
            };
            db.PushDeviceRegistrations.Add(device);
        }

        device.Platform = request.Platform.Trim().ToLowerInvariant();
        device.PushToken = request.PushToken.Trim();
        device.UpdatedAtUtc = now.UtcDateTime;
        device.LastSeenAtUtc = now.UtcDateTime;
        device.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        return new(device.InstallationId, device.Platform, now);
    }

    public async Task<bool> UnregisterAsync(string userId, string installationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(installationId);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var device = await db.PushDeviceRegistrations.SingleOrDefaultAsync(
            x => x.UserId == userId && x.InstallationId == installationId,
            cancellationToken);
        if (device is null) return false;
        device.IsActive = false;
        device.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<PushDeviceTarget>> GetActiveTargetsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PushDeviceRegistrations.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => new PushDeviceTarget(x.Id, x.InstallationId, x.Platform, x.PushToken))
            .ToListAsync(cancellationToken);
    }

    public async Task DeactivateAsync(string pushToken, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.PushDeviceRegistrations.Where(x => x.PushToken == pushToken && x.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UpdatedAtUtc, timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
    }

    private static void Validate(RegisterPushDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InstallationId) || request.InstallationId.Length > 120)
            throw new ArgumentException("Installation ID is required and must not exceed 120 characters.");
        if (!SupportedPlatforms.Contains(request.Platform.Trim().ToLowerInvariant()))
            throw new ArgumentException("Unsupported push platform.");
        if (string.IsNullOrWhiteSpace(request.PushToken) || request.PushToken.Length > 4096)
            throw new ArgumentException("Push token is required and must not exceed 4096 characters.");
    }
}
