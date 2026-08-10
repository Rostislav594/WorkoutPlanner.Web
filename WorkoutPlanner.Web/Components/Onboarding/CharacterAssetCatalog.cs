namespace WorkoutPlanner.Web.Components.Onboarding;

public sealed class CharacterAssetCatalog
{
    public const string ReferenceImage =
        "/_content/WorkoutPlanner.UI/images/character-shrug.png";

    public string Get(CharacterPose pose) => ReferenceImage;

    public static IReadOnlyList<string> PlannedFiles { get; } =
    [
        "character-neutral.png",
        "character-waving.png",
        "character-pointing-left.png",
        "character-pointing-right.png",
        "character-excited.png",
        "character-thinking.png",
        "character-confident.png",
        "character-success.png",
        "character-goodbye.png"
    ];
}
