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
    public string Label { get; set; } = string.Empty;
    public decimal Percent { get; set; }
}
