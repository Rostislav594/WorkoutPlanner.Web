namespace GymPlanner.Mobile.SystemControls;

public sealed record SystemChoiceOption(string Value, string Label);

public interface ISystemChoicePicker
{
    Task<string?> PickAsync(
        string title,
        IReadOnlyList<SystemChoiceOption> options,
        string? selectedValue = null);
}
