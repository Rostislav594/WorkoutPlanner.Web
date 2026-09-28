using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Offline;

/// <summary>Сохранённые дни календаря — все, что приходили с сервера.</summary>
public sealed record OfflineCalendar(IReadOnlyDictionary<int, WorkoutDayApiResponse> Days)
{
    public static OfflineCalendar Empty { get; } = new(new Dictionary<int, WorkoutDayApiResponse>());

    public IReadOnlyList<WorkoutDayApiResponse> InRange(DateTime from, DateTime to) =>
        Days.Values
            .Where(x => x.Date.Date >= from.Date && x.Date.Date <= to.Date)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Id)
            .ToList();

    /// <summary>
    /// Ответ сервера за период полностью заменяет сохранённые дни этого периода:
    /// дни, удалённые на другом устройстве, тоже исчезают.
    /// </summary>
    public OfflineCalendar WithRange(DateTime from, DateTime to, IEnumerable<WorkoutDayApiResponse> days)
    {
        var updated = Days.Values
            .Where(x => x.Date.Date < from.Date || x.Date.Date > to.Date)
            .ToDictionary(x => x.Id);
        foreach (var day in days)
            updated[day.Id] = day;

        return new(updated);
    }

    /// <summary>На одну дату назначается одна тренировка, как и на сервере.</summary>
    public OfflineCalendar Upsert(WorkoutDayApiResponse day)
    {
        var updated = Days.Values
            .Where(x => x.Id != day.Id && x.Date.Date != day.Date.Date)
            .ToDictionary(x => x.Id);
        updated[day.Id] = day;
        return new(updated);
    }

    public OfflineCalendar Remove(int dayId)
    {
        var updated = new Dictionary<int, WorkoutDayApiResponse>(Days);
        updated.Remove(dayId);
        return new(updated);
    }

    /// <summary>Удалённый план забирает с собой свои дни — сервер делает так же.</summary>
    public OfflineCalendar RemovePlan(int trainingPlanId) =>
        new(Days.Values.Where(x => x.TrainingPlanId != trainingPlanId).ToDictionary(x => x.Id));
}
