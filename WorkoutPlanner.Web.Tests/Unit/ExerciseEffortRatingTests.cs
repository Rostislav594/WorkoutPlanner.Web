using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkoutPlanner.Localization;
using WorkoutPlanner.UI.Components;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class ExerciseEffortRatingTests
{
    [Theory]
    [InlineData("NotCompleted", 0)]
    [InlineData("Easy", 1)]
    [InlineData("Medium", 1)]
    [InlineData("Hard", 1)]
    [InlineData("Max", 1)]
    public async Task Editor_UsesFourExclusiveButtons_AndRestoresStoredRating(string value, int selected)
    {
        var html = await RenderAsync(value, false);
        Assert.Equal(4, Regex.Matches(html, "<button\\b").Count);
        Assert.Equal(selected, Regex.Matches(html, "aria-pressed=\"true\"").Count);
        Assert.Equal(4, Regex.Matches(html, "<svg\\b").Count);
        Assert.DoesNotContain("<select", html);
        Assert.DoesNotContain("effort-label", html);
        Assert.Contains("effort-selection", html);
        Assert.Equal(selected, Regex.Matches(html, "effort-selection-text").Count);
        Assert.Equal(selected, Regex.Matches(html, "effort-selection--visible").Count);
        Assert.Contains("Rate the exercise difficulty", html);
        Assert.Contains("role=\"group\"", html);
    }

    [Fact]
    public async Task History_IsNonInteractive_AndKeepsRecordedSelection()
    {
        var html = await RenderAsync("Hard", true);
        Assert.DoesNotContain("<button", html);
        Assert.Single(Regex.Matches(html, "effort-option--selected"));
        Assert.Contains("role=\"img\"", html);
        Assert.Contains("Brutal", html);
        Assert.Contains("effort-option--hard", html);
        Assert.Contains("<svg", html);
    }

    private static async Task<string> RenderAsync(string value, bool readOnly)
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<IAppText>(new AppText(() => "en"));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<ExerciseEffortRating>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { ["Value"] = value, ["ReadOnly"] = readOnly }));
            return output.ToHtmlString();
        });
    }
}
