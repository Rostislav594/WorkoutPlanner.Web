using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.WearOs;

public sealed class WatchPairingService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    UserManager<IdentityUser> userManager,
    IPasswordHasher<WatchPairingCode> codeHasher,
    WatchTokenService tokenService,
    IOptions<WatchPairingOptions> options,
    TimeProvider timeProvider,
    ILogger<WatchPairingService> logger)
{
    public async Task<CreateWatchPairingCodeResponse> CreateCodeAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var activeCodes = await db.WatchPairingCodes
            .Where(x => x.UsedAtUtc == null && x.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        foreach (var previous in activeCodes.Where(x => x.UserId == userId))
            previous.UsedAtUtc = now;

        string code;
        for (var attempt = 0; ; attempt++)
        {
            if (attempt >= 20)
                throw new InvalidOperationException("A unique pairing code could not be generated.");

            code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            if (activeCodes.All(x => !CodeMatches(x, code)))
                break;
        }

        var pairingCode = new WatchPairingCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(options.Value.PairingCodeLifetime)
        };
        pairingCode.CodeHash = codeHasher.HashPassword(pairingCode, code);
        db.WatchPairingCodes.Add(pairingCode);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Created watch pairing code {PairingCodeId} for user {UserId}; expires at {ExpiresAtUtc}.",
            pairingCode.Id,
            userId,
            pairingCode.ExpiresAtUtc);
        return new CreateWatchPairingCodeResponse(code, pairingCode.ExpiresAtUtc);
    }

    public async Task<PairWatchResult> PairAsync(
        PairWatchRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var candidates = await db.WatchPairingCodes
            .Where(x => x.UsedAtUtc == null &&
                        x.CreatedAtUtc >= now.Subtract(
                            options.Value.PairingCodeLifetime * 2))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var pairingCode = candidates.FirstOrDefault(x => CodeMatches(x, request.Code));
        if (pairingCode is null)
        {
            logger.LogWarning(
                "Rejected watch pairing attempt for device identifier hash {DeviceIdHash}: code did not match.",
                ShortHash(request.DeviceId));
            return PairWatchResult.InvalidCode();
        }

        pairingCode.AttemptCount++;
        if (pairingCode.ExpiresAtUtc <= now)
        {
            pairingCode.UsedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PairWatchResult.ExpiredCode();
        }

        if (pairingCode.AttemptCount > options.Value.MaximumPairingAttempts)
        {
            pairingCode.UsedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PairWatchResult.InvalidCode();
        }

        pairingCode.UsedAtUtc = now;
        var deviceId = request.DeviceId.Trim();
        var device = await db.WatchDevices.SingleOrDefaultAsync(
            x => x.DeviceId == deviceId,
            cancellationToken);
        if (device is not null && device.UserId != pairingCode.UserId)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogWarning(
                "Rejected cross-account watch pairing for device {WatchDeviceId}.",
                device.Id);
            return PairWatchResult.DeviceAlreadyPaired();
        }

        var user = await userManager.FindByIdAsync(pairingCode.UserId);
        if (user is null)
            return PairWatchResult.InvalidCode();

        if (device is null)
        {
            device = new WatchDevice
            {
                Id = Guid.NewGuid(),
                UserId = pairingCode.UserId,
                DeviceId = deviceId,
                CreatedAtUtc = now
            };
            db.WatchDevices.Add(device);
        }

        device.DisplayName = request.DisplayName.Trim();
        device.Platform = "WearOS";
        device.DeviceModel = NormalizeOptional(request.DeviceModel, 120);
        device.AppVersion = NormalizeOptional(request.AppVersion, 40);
        device.LastSeenAtUtc = now;
        device.RevokedAtUtc = null;

        var issued = await tokenService.IssueAsync(user, device);
        device.RefreshTokenHash = issued.RefreshTokenHash;
        device.RefreshTokenExpiresAtUtc = issued.RefreshTokenExpiresAtUtc;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Paired watch device {WatchDeviceId} for user {UserId}.",
            device.Id,
            device.UserId);
        return PairWatchResult.Success(issued.Response);
    }

    public async Task<RefreshWatchResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (!tokenService.TryReadRefreshToken(refreshToken, out var payload) ||
            payload is null)
            return RefreshWatchResult.Invalid();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var device = await db.WatchDevices.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == payload.DeviceId,
            cancellationToken);
        if (device is null ||
            device.RevokedAtUtc is not null ||
            device.RefreshTokenExpiresAtUtc <= now ||
            payload.ExpiresAtUtc <= now ||
            !WatchTokenService.HashMatches(refreshToken, device.RefreshTokenHash))
            return RefreshWatchResult.Invalid();

        var user = await userManager.FindByIdAsync(device.UserId);
        if (user is null ||
            !string.Equals(
                user.SecurityStamp ?? string.Empty,
                payload.SecurityStamp,
                StringComparison.Ordinal))
        {
            await db.WatchDevices
                .Where(x => x.Id == device.Id && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.RevokedAtUtc, now),
                    cancellationToken);
            return RefreshWatchResult.Invalid();
        }

        var issued = await tokenService.IssueAsync(user, device);
        var updated = await db.WatchDevices
            .Where(x => x.Id == device.Id &&
                        x.RevokedAtUtc == null &&
                        x.RefreshTokenHash == device.RefreshTokenHash &&
                        x.RefreshTokenExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RefreshTokenHash, issued.RefreshTokenHash)
                    .SetProperty(
                        x => x.RefreshTokenExpiresAtUtc,
                        issued.RefreshTokenExpiresAtUtc)
                    .SetProperty(x => x.LastSeenAtUtc, now),
                cancellationToken);
        return updated == 1
            ? RefreshWatchResult.Success(issued.Response)
            : RefreshWatchResult.Invalid();
    }

    public async Task<IReadOnlyList<WatchDeviceResponse>> GetDevicesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.WatchDevices
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.RevokedAtUtc != null)
            .ThenByDescending(x => x.LastSeenAtUtc ?? x.CreatedAtUtc)
            .Select(x => new WatchDeviceResponse(
                x.Id,
                x.DeviceId,
                x.DisplayName,
                x.Platform,
                x.CreatedAtUtc,
                x.LastSeenAtUtc,
                x.RevokedAtUtc != null,
                x.AppVersion,
                x.DeviceModel))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var device = await db.WatchDevices.SingleOrDefaultAsync(
            x => x.UserId == userId && x.DeviceId == deviceId,
            cancellationToken);
        if (device is null)
            return false;

        if (device.RevokedAtUtc is null)
        {
            device.RevokedAtUtc = now;
            device.RefreshTokenHash = WatchTokenService.Hash(
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            device.RefreshTokenExpiresAtUtc = now.AddTicks(1);
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Revoked watch device {WatchDeviceId} for user {UserId}.",
            device.Id,
            userId);
        return true;
    }

    private bool CodeMatches(WatchPairingCode entity, string code) =>
        codeHasher.VerifyHashedPassword(entity, entity.CodeHash, code) !=
        PasswordVerificationResult.Failed;

    private static string? NormalizeOptional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }

    private static string ShortHash(string value) =>
        Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..12];
}

public enum PairWatchFailure
{
    None,
    InvalidCode,
    ExpiredCode,
    DeviceAlreadyPaired
}

public sealed record PairWatchResult(
    PairWatchFailure Failure,
    WatchTokenResponse? Tokens)
{
    public bool Succeeded => Failure == PairWatchFailure.None;
    public static PairWatchResult Success(WatchTokenResponse tokens) => new(PairWatchFailure.None, tokens);
    public static PairWatchResult InvalidCode() => new(PairWatchFailure.InvalidCode, null);
    public static PairWatchResult ExpiredCode() => new(PairWatchFailure.ExpiredCode, null);
    public static PairWatchResult DeviceAlreadyPaired() => new(PairWatchFailure.DeviceAlreadyPaired, null);
}

public sealed record RefreshWatchResult(WatchTokenResponse? Tokens)
{
    public bool Succeeded => Tokens is not null;
    public static RefreshWatchResult Success(WatchTokenResponse tokens) => new(tokens);
    public static RefreshWatchResult Invalid() => new((WatchTokenResponse?)null);
}
