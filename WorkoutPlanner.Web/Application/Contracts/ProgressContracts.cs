namespace WorkoutPlanner.Web.Application.Contracts;

public sealed class ProgressSnapshot
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string WorkoutName { get; set; } = string.Empty;
    public double Score { get; set; }
}

public sealed class ProgressChartPoint
{
    public DateTime Date { get; set; }
    public decimal Percent { get; set; }
}

/// <summary>Рабочий вес упражнения по последним тренировкам, от старых к новым.</summary>
public sealed record ExerciseWeightTrend(
    string WorkoutName,
    string ExerciseName,
    double CurrentWeight,
    double WeightChange,
    IReadOnlyList<double> Weights);

/// <summary>Изменение последней тренировки относительно предыдущей.</summary>
public sealed record WorkoutScoreChange(
    int TrainingPlanId,
    string WorkoutName,
    decimal ChangePercent,
    IReadOnlyList<double> Scores);

/// <summary>Оба списка идут от самого большого прироста к самому сильному спаду.</summary>
public sealed record ProgressOverview(
    IReadOnlyList<ExerciseWeightTrend> Exercises,
    IReadOnlyList<WorkoutScoreChange> Workouts);
