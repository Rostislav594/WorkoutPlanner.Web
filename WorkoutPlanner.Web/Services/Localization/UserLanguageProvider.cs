using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Localization;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Data;

namespace WorkoutPlanner.Web.Services.Localization;

/// <summary>
/// Язык пользователя из профиля. Нужен там, где текст формируется вне запроса
/// самого пользователя: push-уведомления и ответы поддержки.
/// </summary>
public sealed class UserLanguageProvider(IDbContextFactory<WorkoutDbContext> dbFactory)
    : IUserLanguageProvider
{
    public async Task<string> GetAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return AppLanguages.Default;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var language = await db.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.PreferredLanguage)
            .FirstOrDefaultAsync(cancellationToken);

        return AppLanguages.Resolve(language);
    }
}
