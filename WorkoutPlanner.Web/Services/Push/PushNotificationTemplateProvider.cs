using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Services.Push;

public sealed class PushNotificationTemplateProvider :
    IPushNotificationTemplateProvider
{
    public PushNotificationTemplate Get(PushNotificationType type) => type switch
    {
        PushNotificationType.SupportReply => new(
            "GPlanner",
            "Вам пришёл ответ от службы поддержки."),
        PushNotificationType.InboxMessage => new(
            "GPlanner",
            "У вас новое сообщение."),
        PushNotificationType.AppUpdate => new(
            "GPlanner",
            "Для вас доступно новое обновление."),
        PushNotificationType.AccountNotification => new(
            "GPlanner",
            "У вас новое уведомление аккаунта."),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
