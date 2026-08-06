namespace GymPlanner.Mobile.Authentication;

public sealed record MobileAuthResult(
    bool Succeeded,
    IReadOnlyList<string> Errors)
{
    public static MobileAuthResult Success { get; } = new(true, []);

    public static MobileAuthResult Failure(params string[] errors) =>
        new(false, errors);
}
