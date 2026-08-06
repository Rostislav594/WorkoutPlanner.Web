namespace WorkoutPlanner.Web.Components.Onboarding;

public sealed class GuideStep
{
    public string Id { get; init; } = string.Empty;
    public GuideSection Section { get; init; }
    public string Route { get; init; } = string.Empty;
    public Func<CancellationToken, Task<string>>? ResolveRouteAsync { get; init; }
    public string? Target { get; init; }
    public GuideDisplayMode DisplayMode { get; init; }
    public string Message { get; init; } = string.Empty;
    public CharacterEmotion Emotion { get; init; }
    public CharacterPose Pose { get; init; }
    public string CharacterImage { get; init; } = string.Empty;
    public GuidePlacement Placement { get; init; }
    public Func<CancellationToken, Task>? BeforeShowAsync { get; init; }
    public string? NextStepId { get; init; }
    public IReadOnlyList<GuideChoice> Choices { get; init; } = [];
    public string? ExpectedActionId { get; init; }
    public bool AllowTargetInteraction { get; init; }
    public bool AutoAdvanceOnAction { get; init; }
    public bool IsOptional { get; init; }
    public bool IsFinal { get; init; }
    public bool AllowBack { get; init; } = true;
    public string PrimaryButtonText { get; init; } = "Далее";
    public string? StableResumeStepId { get; init; }
}

public sealed class GuideChoice
{
    public string Id { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string NextStepId { get; init; } = string.Empty;
    public string? Outcome { get; init; }
    public Func<CancellationToken, Task>? OnSelectedAsync { get; init; }
}

public sealed class AppGuideScenario
{
    private readonly IReadOnlyDictionary<string, GuideStep> _stepsById;

    public AppGuideScenario(IReadOnlyList<GuideStep> steps)
    {
        Steps = steps;
        _stepsById = steps.ToDictionary(x => x.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<GuideStep> Steps { get; }

    public GuideStep? Find(string? id)
    {
        return id is not null && _stepsById.TryGetValue(id, out var step)
            ? step
            : null;
    }
}

public enum GuideSection { Navigation, Practice }
public enum GuideDisplayMode { PageOverview, Spotlight }
public enum CharacterEmotion { Neutral, Friendly, Excited, Thinking, Confident, Playful, Supportive, Success }
public enum CharacterPose { Open, Waving, PointingLeft, PointingRight, Inviting, Explaining, ThumbsUp, Goodbye, Shrug }
public enum GuidePlacement { Auto, Left, Right, Above, Below, Center }
