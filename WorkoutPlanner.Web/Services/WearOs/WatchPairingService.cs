using System.Buffers.Text;
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
        var issueResult = await IssueForDeviceAsync(
            db,
            pairingCode.UserId,
            request.DeviceId,
            request.DisplayName,
            request.DeviceModel,
            request.AppVersion,
            now,
            cancellationToken);
        if (!issueResult.Succeeded)
        {
            // Причину отказа всё равно фиксируем: код уже потрачен.
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return issueResult.Failure == IssueDeviceFailure.DeviceOwnedByAnotherUser
                ? PairWatchResult.DeviceAlreadyPaired()
                : PairWatchResult.InvalidCode();
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PairWatchResult.Success(issueResult.Tokens!);
    }

    /// <summary>
    /// Создаёт или переиспользует устройство и выдаёт ему пару токенов.
    /// Общий хвост обоих способов сопряжения: ввода кода и подтверждения с телефона.
    /// Вызывающий сам решает, когда делать SaveChanges и commit.
    /// </summary>
    private async Task<IssueDeviceResult> IssueForDeviceAsync(
        WorkoutDbContext db,
        string userId,
        string requestedDeviceId,
        string requestedDisplayName,
        string? deviceModel,
        string? appVersion,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var deviceId = requestedDeviceId.Trim();
        var device = await db.WatchDevices.SingleOrDefaultAsync(
            x => x.DeviceId == deviceId,
            cancellationToken);
        if (device is not null && device.UserId != userId)
        {
            logger.LogWarning(
                "Rejected cross-account watch pairing for device {WatchDeviceId}.",
                device.Id);
            return IssueDeviceResult.Failed(IssueDeviceFailure.DeviceOwnedByAnotherUser);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return IssueDeviceResult.Failed(IssueDeviceFailure.UserNotFound);

        if (device is null)
        {
            device = new WatchDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeviceId = deviceId,
                CreatedAtUtc = now
            };
            db.WatchDevices.Add(device);
        }

        device.DisplayName = requestedDisplayName.Trim();
        device.Platform = "WearOS";
        device.DeviceModel = NormalizeOptional(deviceModel, 120);
        device.AppVersion = NormalizeOptional(appVersion, 40);
        device.LastSeenAtUtc = now;
        device.RevokedAtUtc = null;

        var issued = await tokenService.IssueAsync(user, device);
        device.RefreshTokenHash = issued.RefreshTokenHash;
        device.RefreshTokenExpiresAtUtc = issued.RefreshTokenExpiresAtUtc;

        logger.LogInformation(
            "Paired watch device {WatchDeviceId} for user {UserId}.",
            device.Id,
            device.UserId);
        return IssueDeviceResult.Success(issued.Response);
    }

    /// <summary>
    /// Часы создают заявку до всякой аутентификации: кому они принадлежат, станет
    /// известно только после подтверждения на телефоне.
    /// </summary>
    public async Task<StartWatchPairingResponse> StartRequestAsync(
        StartWatchPairingRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var requestId = CreateSecret();
        var pollToken = CreateSecret();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.WatchPairingRequests.Add(new WatchPairingRequest
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            PollTokenHash = HashSecret(pollToken),
            DeviceId = request.DeviceId.Trim(),
            DisplayName = request.DisplayName.Trim(),
            DeviceModel = NormalizeOptional(request.DeviceModel, 120),
            AppVersion = NormalizeOptional(request.AppVersion, 40),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(options.Value.PairingRequestLifetime)
        });
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created watch pairing request for device identifier hash {DeviceIdHash}.",
            ShortHash(request.DeviceId));

        return new StartWatchPairingResponse(
            requestId,
            pollToken,
            options.Value.ApproveUrlTemplate.Replace(
                WatchPairingOptions.RequestIdPlaceholder,
                Uri.EscapeDataString(requestId),
                StringComparison.Ordinal),
            now.Add(options.Value.PairingRequestLifetime));
    }

    /// <summary>Данные для экрана подтверждения на телефоне.</summary>
    public async Task<WatchPairingRequestDetailsResponse?> GetRequestAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.WatchPairingRequests.AsNoTracking().SingleOrDefaultAsync(
            x => x.RequestId == requestId,
            cancellationToken);
        if (entity is null)
            return null;

        return new WatchPairingRequestDetailsResponse(
            entity.RequestId,
            entity.DisplayName,
            entity.DeviceModel,
            entity.CreatedAtUtc,
            entity.ExpiresAtUtc,
            DescribeStatus(entity, now));
    }

    public async Task<WatchPairingDecisionResult> ApproveRequestAsync(
        string userId,
        string requestId,
        CancellationToken cancellationToken) =>
        await DecideAsync(userId, requestId, approve: true, cancellationToken);

    public async Task<WatchPairingDecisionResult> RejectRequestAsync(
        string userId,
        string requestId,
        CancellationToken cancellationToken) =>
        await DecideAsync(userId, requestId, approve: false, cancellationToken);

    private async Task<WatchPairingDecisionResult> DecideAsync(
        string userId,
        string requestId,
        bool approve,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var entity = await db.WatchPairingRequests.SingleOrDefaultAsync(
            x => x.RequestId == requestId,
            cancellationToken);
        if (entity is null)
            return WatchPairingDecisionResult.Failed(WatchPairingRequestFailure.NotFound);
        if (entity.ApprovedAtUtc is not null ||
            entity.RejectedAtUtc is not null ||
            entity.CompletedAtUtc is not null)
        {
            return WatchPairingDecisionResult.Failed(
                WatchPairingRequestFailure.AlreadyResolved);
        }

        if (entity.ExpiresAtUtc <= now)
            return WatchPairingDecisionResult.Failed(WatchPairingRequestFailure.Expired);

        if (!approve)
        {
            entity.RejectedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "User {UserId} rejected a watch pairing request.",
                userId);
            return WatchPairingDecisionResult.Success();
        }

        // Чужое устройство не переносится между аккаунтами. Проверяем здесь, чтобы
        // человек увидел отказ на телефоне, а не молча на часах при опросе.
        var deviceId = entity.DeviceId;
        var conflicting = await db.WatchDevices.AsNoTracking().AnyAsync(
            x => x.DeviceId == deviceId && x.UserId != userId,
            cancellationToken);
        if (conflicting)
        {
            return WatchPairingDecisionResult.Failed(
                WatchPairingRequestFailure.DeviceOwnedByAnotherUser);
        }

        entity.ApprovedByUserId = userId;
        entity.ApprovedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "User {UserId} approved a watch pairing request.",
            userId);
        return WatchPairingDecisionResult.Success();
    }

    /// <summary>
    /// Опрос со стороны часов. Токены выдаются ровно один раз: повторный опрос
    /// после выдачи считается исчерпанной заявкой.
    /// </summary>
    public async Task<WatchPairingStatusResult> PollRequestAsync(
        string requestId,
        string pollToken,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var entity = await db.WatchPairingRequests.SingleOrDefaultAsync(
            x => x.RequestId == requestId,
            cancellationToken);
        if (entity is null || !SecretMatches(entity.PollTokenHash, pollToken))
            return WatchPairingStatusResult.Failed(WatchPairingRequestFailure.NotFound);

        entity.PollCount++;

        if (entity.CompletedAtUtc is not null || entity.ExpiresAtUtc <= now)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return WatchPairingStatusResult.Success(WatchPairingStatuses.Expired, null);
        }

        if (entity.RejectedAtUtc is not null)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return WatchPairingStatusResult.Success(WatchPairingStatuses.Rejected, null);
        }

        if (entity.ApprovedByUserId is null)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return WatchPairingStatusResult.Success(WatchPairingStatuses.Pending, null);
        }

        var issueResult = await IssueForDeviceAsync(
            db,
            entity.ApprovedByUserId,
            entity.DeviceId,
            entity.DisplayName,
            entity.DeviceModel,
            entity.AppVersion,
            now,
            cancellationToken);
        if (!issueResult.Succeeded)
        {
            // Заявку закрываем: повторять её бессмысленно, нужна новая.
            entity.CompletedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return WatchPairingStatusResult.Failed(
                issueResult.Failure == IssueDeviceFailure.DeviceOwnedByAnotherUser
                    ? WatchPairingRequestFailure.DeviceOwnedByAnotherUser
                    : WatchPairingRequestFailure.NotFound);
        }

        entity.CompletedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return WatchPairingStatusResult.Success(
            WatchPairingStatuses.Approved,
            issueResult.Tokens);
    }

    private static string DescribeStatus(WatchPairingRequest entity, DateTime now)
    {
        if (entity.RejectedAtUtc is not null)
            return WatchPairingStatuses.Rejected;
        if (entity.CompletedAtUtc is not null || entity.ExpiresAtUtc <= now)
            return WatchPairingStatuses.Expired;
        return entity.ApprovedByUserId is null
            ? WatchPairingStatuses.Pending
            : WatchPairingStatuses.Approved;
    }

    private static string CreateSecret() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string HashSecret(string secret) =>
        Convert.ToBase64String(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)));

    private static bool SecretMatches(string expectedHash, string secret)
    {
        var actual = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
        Span<byte> expected = stackalloc byte[SHA256.HashSizeInBytes];
        return Convert.TryFromBase64String(expectedHash, expected, out var written) &&
               written == SHA256.HashSizeInBytes &&
               CryptographicOperations.FixedTimeEquals(expected, actual);
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

    public async Task<WatchDeviceResponse?> RenameAsync(
        string userId,
        string deviceId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var normalizedName = displayName.Trim();
        if (normalizedName.Length is 0 or > 120)
            throw new ArgumentException("Display name must contain from 1 to 120 characters.", nameof(displayName));

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var updated = await db.WatchDevices
            .Where(x => x.UserId == userId &&
                        x.DeviceId == deviceId &&
                        x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.DisplayName, normalizedName),
                cancellationToken);
        if (updated != 1)
            return null;

        var device = await db.WatchDevices
            .AsNoTracking()
            .SingleAsync(
                x => x.UserId == userId && x.DeviceId == deviceId,
                cancellationToken);
        logger.LogInformation(
            "Renamed watch device {WatchDeviceId} for user {UserId}.",
            device.Id,
            userId);
        return ToResponse(device);
    }

    public async Task<int> RevokeAllAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var devices = await db.WatchDevices
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            device.RevokedAtUtc = now;
            device.RefreshTokenHash = WatchTokenService.Hash(
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            device.RefreshTokenExpiresAtUtc = now.AddTicks(1);
        }

        if (devices.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Revoked {WatchDeviceCount} watch devices for user {UserId}.",
            devices.Count,
            userId);
        return devices.Count;
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

    private static WatchDeviceResponse ToResponse(WatchDevice device) =>
        new(
            device.Id,
            device.DeviceId,
            device.DisplayName,
            device.Platform,
            device.CreatedAtUtc,
            device.LastSeenAtUtc,
            device.RevokedAtUtc is not null,
            device.AppVersion,
            device.DeviceModel);

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

internal enum IssueDeviceFailure
{
    None,
    UserNotFound,
    DeviceOwnedByAnotherUser
}

internal sealed record IssueDeviceResult(
    IssueDeviceFailure Failure,
    WatchTokenResponse? Tokens)
{
    public bool Succeeded => Failure == IssueDeviceFailure.None;
    public static IssueDeviceResult Success(WatchTokenResponse tokens) =>
        new(IssueDeviceFailure.None, tokens);
    public static IssueDeviceResult Failed(IssueDeviceFailure failure) =>
        new(failure, null);
}

public enum WatchPairingRequestFailure
{
    None,
    NotFound,
    Expired,
    AlreadyResolved,
    DeviceOwnedByAnotherUser
}

public sealed record WatchPairingDecisionResult(WatchPairingRequestFailure Failure)
{
    public bool Succeeded => Failure == WatchPairingRequestFailure.None;
    public static WatchPairingDecisionResult Success() =>
        new(WatchPairingRequestFailure.None);
    public static WatchPairingDecisionResult Failed(WatchPairingRequestFailure failure) =>
        new(failure);
}

public sealed record WatchPairingStatusResult(
    WatchPairingRequestFailure Failure,
    string? Status,
    WatchTokenResponse? Tokens)
{
    public bool Succeeded => Failure == WatchPairingRequestFailure.None;
    public static WatchPairingStatusResult Success(
        string status,
        WatchTokenResponse? tokens) =>
        new(WatchPairingRequestFailure.None, status, tokens);
    public static WatchPairingStatusResult Failed(WatchPairingRequestFailure failure) =>
        new(failure, null, null);
}
