using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Admin;
using WorkoutPlanner.Web.Services.Auth;
using WorkoutPlanner.Web.Services.Localization;
using WorkoutPlanner.Web.Services.Push;

namespace WorkoutPlanner.Web.Services;

public sealed class AdminPublicationService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    CurrentUserService currentUser,
    UserManager<IdentityUser> userManager,
    IInboxPublicationImageStorage imageStorage,
    IRemotePushProvider pushProvider,
    TimeProvider timeProvider) : IAdminPublicationService
{
    public async Task<AdminPublicationItem> CreateAsync(AdminPublicationDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var adminUserId = await GetRequiredAdminUserIdAsync();
        var (title, body) = Validate(draft);

        string? imagePath = null;
        try
        {
            if (draft.Image is not null) imagePath = await imageStorage.SaveAsync(draft.Image, cancellationToken);
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var entity = new InboxPublication
            {
                Type = draft.Type,
                Title = title,
                Preview = CreatePreview(body),
                Body = body,
                ImagePath = imagePath,
                PublishedAtUtc = DateTime.SpecifyKind(draft.PublishedAtUtc, DateTimeKind.Utc),
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                CreatedByUserId = adminUserId,
                SendPushNotification = draft.SendPushNotification
            };
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.InboxPublications.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
            AddAudit(db, adminUserId, AdminAuditActions.PublicationCreated, entity.Id);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToItem(entity);
        }
        catch
        {
            if (imagePath is not null) await imageStorage.DeleteAsync(imagePath, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<AdminPublicationItem>> GetRecentAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        await GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var publications = await db.InboxPublications.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(count, 1, 100))
            .Select(x => new
            {
                Publication = x,
                ReadCount = x.Reads.Count(read => read.ReadAtUtc != null)
            })
            .ToListAsync(cancellationToken);

        var items = new List<AdminPublicationItem>(publications.Count);
        foreach (var row in publications)
        {
            var audience = await CountAudienceAsync(db, row.Publication.PublishedAtUtc, cancellationToken);
            items.Add(ToItem(row.Publication) with { ReadCount = row.ReadCount, AudienceCount = audience });
        }

        return items;
    }

    public async Task<AdminPublicationItem?> UpdateAsync(
        long id,
        AdminPublicationDraft draft,
        bool removeImage = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var adminUserId = await GetRequiredAdminUserIdAsync();
        var (title, body) = Validate(draft);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.InboxPublications.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return null;

        var oldImagePath = entity.ImagePath;
        string? newImagePath = null;
        try
        {
            if (draft.Image is not null)
                newImagePath = await imageStorage.SaveAsync(draft.Image, cancellationToken);

            entity.Type = draft.Type;
            entity.Title = title;
            entity.Preview = CreatePreview(body);
            entity.Body = body;
            entity.PublishedAtUtc = DateTime.SpecifyKind(draft.PublishedAtUtc, DateTimeKind.Utc);
            if (newImagePath is not null)
                entity.ImagePath = newImagePath;
            else if (removeImage)
                entity.ImagePath = null;
            // Разосланный push не отзывается и не повторяется: флаг меняется,
            // только пока рассылки ещё не было.
            if (entity.PushSentAtUtc is null)
                entity.SendPushNotification = draft.SendPushNotification;

            AddAudit(db, adminUserId, AdminAuditActions.PublicationUpdated, entity.Id);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (newImagePath is not null) await imageStorage.DeleteAsync(newImagePath, cancellationToken);
            throw;
        }

        // Старый файл удаляем только после того, как правка сохранена.
        if (oldImagePath is not null && oldImagePath != entity.ImagePath)
            await imageStorage.DeleteAsync(oldImagePath, cancellationToken);

        return ToItem(entity);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var adminUserId = await GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.InboxPublications.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return false;

        // Отметки о прочтении удаляются каскадом вместе с публикацией.
        db.InboxPublications.Remove(entity);
        AddAudit(db, adminUserId, AdminAuditActions.PublicationDeleted, id);
        await db.SaveChangesAsync(cancellationToken);

        if (entity.ImagePath is not null)
            await imageStorage.DeleteAsync(entity.ImagePath, cancellationToken);

        return true;
    }

    public async Task<int> CountPushDevicesAsync(DateTime publishedAtUtc, CancellationToken cancellationToken = default)
    {
        await GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var publishedAt = DateTime.SpecifyKind(publishedAtUtc, DateTimeKind.Utc);
        return await PublicationPushDispatcher.RecipientDevices(db, publishedAt).CountAsync(cancellationToken);
    }

    public async Task<int?> SendTestPushAsync(long id, CancellationToken cancellationToken = default)
    {
        var adminUserId = await GetRequiredAdminUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var publication = await db.InboxPublications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        // Пока публикация не вышла, телефон не откроет её по нажатию.
        if (publication is null || publication.PublishedAtUtc > timeProvider.GetUtcNow().UtcDateTime)
            return null;

        var devices = await db.PushDeviceRegistrations
            .CountAsync(x => x.UserId == adminUserId && x.IsActive, cancellationToken);
        if (devices > 0)
            await pushProvider.SendAsync(PublicationPushDispatcher.CreateMessage(adminUserId, publication), cancellationToken);
        return devices;
    }

    /// <summary>
    /// Сколько пользователей видят публикацию: зарегистрированные не позже неё,
    /// а также старые аккаунты без даты регистрации (как во «Входящих» на телефоне).
    /// </summary>
    private static Task<int> CountAudienceAsync(
        WorkoutDbContext db,
        DateTime publishedAtUtc,
        CancellationToken cancellationToken) =>
        db.Users.CountAsync(
            user => !db.UserActivities.Any(activity =>
                activity.UserId == user.Id &&
                activity.RegisteredAtUtc != null &&
                activity.RegisteredAtUtc > publishedAtUtc),
            cancellationToken);

    private static (string Title, string Body) Validate(AdminPublicationDraft draft)
    {
        if (draft.Type is not (InboxMessageType.News or InboxMessageType.Update or InboxMessageType.System))
            throw new InvalidDataException(ServerTexts.Current["Server_Publication_InvalidType"]);
        var title = draft.Title.Trim();
        var body = draft.Body.Trim();
        if (title.Length is 0 or > 160 || body.Length is 0 or > 8000)
            throw new InvalidDataException(ServerTexts.Current["Server_Publication_CheckFields"]);
        return (title, body);
    }

    private void AddAudit(WorkoutDbContext db, string adminUserId, string action, long publicationId) =>
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = adminUserId,
            Action = action,
            TargetType = nameof(InboxPublication),
            TargetId = publicationId.ToString(),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });

    private static string CreatePreview(string body) =>
        body.Length <= 400 ? body : $"{body[..397]}…";

    private static AdminPublicationItem ToItem(InboxPublication x) =>
        new(x.Id, x.Type, x.Title, x.Body, x.ImagePath, x.PublishedAtUtc, x.CreatedAtUtc,
            x.SendPushNotification, PushSentAtUtc: x.PushSentAtUtc);

    private async Task<string> GetRequiredAdminUserIdAsync()
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.IsInRoleAsync(user, ApplicationRoles.Admin))
            throw new UnauthorizedAccessException("Administrator role is required.");

        return userId;
    }
}
