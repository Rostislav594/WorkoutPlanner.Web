using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;
using TrainingPlanEntity = WorkoutPlanner.Web.Models.TrainingPlan;
using WorkoutPlanner.Web.Services.Localization;

namespace WorkoutPlanner.Web.Services;

/// <inheritdoc cref="IFreeWorkoutDraftService"/>
public sealed class FreeWorkoutDraftService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    CurrentUserService currentUser,
    TimeProvider timeProvider)
    : IFreeWorkoutDraftService
{
    /// <summary>
    /// Имя черновика. Человеку оно видно на часах, поэтому осмысленное
    /// и на языке запроса: имя сохраняется в план и позже не переводится.
    /// </summary>
    private static string DraftName => ServerTexts.Current["Server_FreeWorkout_Name"];

    public async Task<FreeWorkoutDraftResult> StartAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Повторный старт возвращает тот же черновик, а не заводит второй:
        // запрос может прийти дважды при обрыве связи или перезапуске приложения,
        // и человек не должен получить две параллельные тренировки.
        var existing = await FindDraftAsync(db, userId, cancellationToken);
        if (existing is not null)
            return ToResult(existing, alreadyStarted: true);

        var draft = new TrainingPlanEntity
        {
            UserId = userId,
            WorkoutName = DraftName,
            Date = timeProvider.GetLocalNow().Date,
            IsFreeDraft = true,
        };
        db.TrainingPlans.Add(draft);
        await db.SaveChangesAsync(cancellationToken);

        return ToResult(draft, alreadyStarted: false);
    }

    public async Task<FreeWorkoutDraftResult?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var draft = await FindDraftAsync(db, userId, cancellationToken);
        return draft is null ? null : ToResult(draft, alreadyStarted: true);
    }

    public async Task<bool> DiscardAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var draft = await FindDraftAsync(db, userId, cancellationToken);
        if (draft is null)
            return false;

        // Упражнения и подходы уходят каскадом вместе с планом.
        db.TrainingPlans.Remove(draft);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Черновик пользователя. Их не может быть больше одного, но если старые
    /// данные всё же оставили несколько, берётся последний созданный.
    /// </summary>
    private static Task<TrainingPlanEntity?> FindDraftAsync(
        WorkoutDbContext db,
        string userId,
        CancellationToken cancellationToken) =>
        db.TrainingPlans
            .Where(x => x.UserId == userId && x.IsFreeDraft)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static FreeWorkoutDraftResult ToResult(TrainingPlanEntity draft, bool alreadyStarted) =>
        new(
            draft.Id,
            FreeWorkoutDraftId.FromPlanId(draft.Id),
            draft.WorkoutName,
            alreadyStarted);
}
