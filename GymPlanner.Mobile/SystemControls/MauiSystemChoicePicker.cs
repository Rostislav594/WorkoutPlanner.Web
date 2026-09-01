namespace GymPlanner.Mobile.SystemControls;

public sealed class MauiSystemChoicePicker : ISystemChoicePicker
{
    public Task<string?> PickAsync(
        string title,
        IReadOnlyList<SystemChoiceOption> options,
        string? selectedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
            return Task.FromResult<string?>(null);

        return MainThread.InvokeOnMainThreadAsync(
            () => PickOnMainThreadAsync(title, options, selectedValue));
    }

    private static Task<string?> PickOnMainThreadAsync(
        string title,
        IReadOnlyList<SystemChoiceOption> options,
        string? selectedValue)
    {
#if ANDROID
        return PickAndroidAsync(title, options, selectedValue);
#else
        return PickActionSheetAsync(title, options, selectedValue);
#endif
    }

#if ANDROID
    private static Task<string?> PickAndroidAsync(
        string title,
        IReadOnlyList<SystemChoiceOption> options,
        string? selectedValue)
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
            return Task.FromResult<string?>(null);

        var completion = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var labels = options.Select(option => option.Label).ToArray();
        var selectedIndex = -1;

        for (var index = 0; index < options.Count; index++)
        {
            if (options[index].Value == selectedValue)
            {
                selectedIndex = index;
                break;
            }
        }

        Android.App.AlertDialog? dialog = null;
        var builder = new Android.App.AlertDialog.Builder(activity);
        builder.SetTitle(title);
        builder.SetSingleChoiceItems(labels, selectedIndex, (_, args) =>
        {
            completion.TrySetResult(options[args.Which].Value);
            dialog?.Dismiss();
        });
        builder.SetNegativeButton("Отмена", (_, _) => completion.TrySetResult(null));

        var createdDialog = builder.Create();
        if (createdDialog is null)
            return Task.FromResult<string?>(null);

        dialog = createdDialog;
        createdDialog.CancelEvent += (_, _) => completion.TrySetResult(null);
        createdDialog.Show();

        return completion.Task;
    }
#else
    private static async Task<string?> PickActionSheetAsync(
        string title,
        IReadOnlyList<SystemChoiceOption> options,
        string? selectedValue)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
            return null;

        var labels = options
            .Select(option => option.Value == selectedValue
                ? $"✓ {option.Label}"
                : option.Label)
            .ToArray();

#pragma warning disable CS0618
        var selectedLabel = await page.DisplayActionSheetAsync(
            title,
            "Отмена",
            null,
            labels);
#pragma warning restore CS0618

        if (string.IsNullOrEmpty(selectedLabel) || selectedLabel == "Отмена")
            return null;

        var selectedIndex = Array.IndexOf(labels, selectedLabel);
        return selectedIndex >= 0 ? options[selectedIndex].Value : null;
    }
#endif
}
