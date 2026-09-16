using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Services.Localization;

namespace WorkoutPlanner.Web.Services.Push;

public sealed class PushNotificationTemplateProvider :
    IPushNotificationTemplateProvider
{
    public PushNotificationTemplate Get(PushNotificationType type, string language)
    {
        // Текст берётся на языке получателя, а не запроса: уведомление уходит
        // на устройство пользователя, и читает его он.
        var text = ServerTexts.For(language);
        return type switch
        {
            PushNotificationType.SupportReply => new(
                "GPlanner",
                text["Push_SupportReply"]),
            PushNotificationType.InboxMessage => new(
                "GPlanner",
                text["Push_InboxMessage"]),
            PushNotificationType.AppUpdate => new(
                "GPlanner",
                text["Push_AppUpdate"]),
            PushNotificationType.AccountNotification => new(
                "GPlanner",
                text["Push_AccountNotification"]),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
