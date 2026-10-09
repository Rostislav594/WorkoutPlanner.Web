using System.Text.Json;

namespace GymPlanner.Mobile.Notes;

/// <summary>
/// Заметки блокнота. Живут только на телефоне, в Preferences: их читают
/// страница блокнота и виджет на главной.
/// </summary>
public static class NotebookStorage
{
    private const string NotesKey = "gymplanner.notebook.v1";
    private const string HomeNoteKey = "gymplanner.notebook.home.v1";

    public static List<NotebookNote> Load()
    {
        try
        {
            var json = Preferences.Default.Get(NotesKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
                return JsonSerializer.Deserialize<List<NotebookNote>>(json) ?? [];
        }
        catch (JsonException)
        {
            Preferences.Default.Remove(NotesKey);
        }

        return [];
    }

    public static void Save(IEnumerable<NotebookNote> notes) =>
        Preferences.Default.Set(NotesKey, JsonSerializer.Serialize(notes));

    /// <summary>
    /// Заметка для главной: выбранная пользователем, а если её нет или она
    /// удалена — первая вкладка, с которой открывается блокнот.
    /// </summary>
    public static NotebookNote? FindHomeNote(IReadOnlyList<NotebookNote> notes)
    {
        var homeNoteId = Preferences.Default.Get(HomeNoteKey, string.Empty);
        return notes.FirstOrDefault(x => x.Id.ToString() == homeNoteId)
            ?? notes.FirstOrDefault();
    }

    public static void SetHomeNote(Guid id) =>
        Preferences.Default.Set(HomeNoteKey, id.ToString());
}

public sealed record NotebookNote(Guid Id, string Name, string Content)
{
    public string Content { get; set; } = Content;
}
