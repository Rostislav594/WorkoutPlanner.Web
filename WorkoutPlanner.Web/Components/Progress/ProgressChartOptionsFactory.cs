using ApexCharts;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Components.Progress;

internal static class ProgressChartOptionsFactory
{
    public static ApexChartOptions<ProgressChartPoint> Create(decimal min, decimal max) => new()
    {
        Chart = new Chart
        {
            Type = ChartType.Line,
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false }
        },
        Colors = ["#3fb950"],
        DataLabels = new DataLabels { Enabled = false },
        Stroke = new Stroke
        {
            Show = true,
            Curve = Curve.Straight,
            Width = 3
        },
        Fill = new Fill
        {
            Type = FillType.Solid,
            Opacity = 1
        },
        Markers = new Markers
        {
            Size = 4,
            StrokeWidth = 2,
            StrokeColors = ["#ffffff"],
            Hover = new MarkersHover { SizeOffset = 3 }
        },
        Yaxis =
        [
            new YAxis
            {
                Min = (double)min,
                Max = (double)max,
                TickAmount = 4,
                Labels = new YAxisLabels
                {
                    Formatter = "function(value) { return Math.round(value * 100) / 100 + '%'; }",
                    Style = new AxisLabelStyle { Colors = ["#d0d0d0"] }
                }
            }
        ],
        Tooltip = new Tooltip
        {
            Enabled = true,
            Shared = false,
            Intersect = false,
            X = new TooltipX { Format = "dd.MM.yyyy" },
            Y = new TooltipY
            {
                Formatter = "function(value) { return Math.round(value * 100) / 100 + '%'; }"
            }
        },
        Xaxis = new XAxis
        {
            Type = XAxisType.Datetime,
            Labels = new XAxisLabels
            {
                DatetimeUTC = false,
                Format = "dd.MM",
                HideOverlappingLabels = true,
                Rotate = 0,
                Style = new AxisLabelStyle { Colors = ["#d0d0d0"] }
            }
        },
        Grid = new Grid { BorderColor = "#202020" }
    };
}
