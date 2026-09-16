namespace GymPlanner.Mobile.Localization;

/// <summary>
/// Тип сообщения из inbox — в ключ ресурса.
/// </summary>
/// <remarks>
/// Тип приходит с сервера строкой и переводится на клиенте: так текст
/// не зависит от языка сервера. Неизвестный тип считается системным
/// сообщением — сервер может завести новый тип раньше, чем обновится клиент.
/// </remarks>
public static class InboxMessageTypes
{
    public static string KeyFor(string? type) => type switch
    {
        "SupportReply" => "InboxType_SupportReply",
        "News" => "InboxType_News",
        "Update" => "InboxType_Update",
        _ => "InboxType_System"
    };
}
