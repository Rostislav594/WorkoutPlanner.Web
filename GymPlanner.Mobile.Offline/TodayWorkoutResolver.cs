using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Собирает сегодняшнюю тренировку из сохранённых календаря и планов —
/// так же, как сервер выбирает её в <c>GET /workouts/today</c>.
/// </summary>
public static class TodayWorkoutResolver
{
    public static TodayWorkoutApiResponse? Resolve(
        OfflineCalendar calendar,
        OfflinePlans plans,
        DateTime today)
    {
        var day = calendar.Days.Values
            .Where(x => x.Date.Date == today.Date && !x.IsCompleted)
            .OrderBy(x => x.Id)
            .FirstOrDefault();
        if (day is null)
            return null;

        var plan = plans.Find(day.TrainingPlanId);
        return plan is null ? null : new TodayWorkoutApiResponse(day, plan);
    }
}
