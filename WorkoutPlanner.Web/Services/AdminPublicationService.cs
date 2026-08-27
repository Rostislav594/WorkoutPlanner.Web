using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Admin;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public sealed class AdminPublicationService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    CurrentUserService currentUser,
    UserManager<IdentityUser> userManager,
    IInboxPublicationImageStorage imageStorage,
    TimeProvider timeProvider) : IAdminPublicationService
{
    public async Task<AdminPublicationItem> CreateAsync(AdminPublicationDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var adminUserId = await GetRequiredAdminUserIdAsync();
        if (draft.Type is not (InboxMessageType.News or InboxMessageType.Update or InboxMessageType.System))
            throw new InvalidDataException("Недопустимый тип публикации.");
        var title = draft.Title.Trim();
        var body = draft.Body.Trim();
        if (title.Length is 0 or > 160 || body.Length is 0 or > 8000)
            throw new InvalidDataException("Проверьте заголовок и текст публикации.");

        string? imagePath = null;
        try
        {
            if (draft.Image is not null) imagePath = await imageStorage.SaveAsync(draft.Image, cancellationToken);
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var entity = new InboxPublication
            {
                Type = draft.Type,
                Title = title,
                Preview = body.Length <= 400 ? body : $"{body[..397]}…",
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
            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                AdminUserId = adminUserId,
                Action = AdminAuditActions.PublicationCreated,
                TargetType = nameof(InboxPublication),
                TargetId = entity.Id.ToString(),
                CreatedAtUtc = entity.CreatedAtUtc
            });
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
        return await db.InboxPublications.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(count, 1, 100))
            .Select(x => new AdminPublicationItem(x.Id, x.Type, x.Title, x.Body, x.ImagePath, x.PublishedAtUtc, x.CreatedAtUtc, x.SendPushNotification))
            .ToListAsync(cancellationToken);
    }

    private static AdminPublicationItem ToItem(InboxPublication x) =>
        new(x.Id, x.Type, x.Title, x.Body, x.ImagePath, x.PublishedAtUtc, x.CreatedAtUtc, x.SendPushNotification);

    private async Task<string> GetRequiredAdminUserIdAsync()
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.IsInRoleAsync(user, ApplicationRoles.Admin))
            throw new UnauthorizedAccessException("Administrator role is required.");

        return userId;
    }
}
