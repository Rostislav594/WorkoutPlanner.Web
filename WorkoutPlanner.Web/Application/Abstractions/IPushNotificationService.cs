using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IPushNotificationTemplateProvider
{
    /// <param name="language">
    /// Язык получателя из его профиля: push формируется на сервере, а читает
    /// его пользователь, поэтому культура запроса здесь не подходит.
    /// </param>
    PushNotificationTemplate Get(PushNotificationType type, string language);
}

/// <summary>Язык интерфейса пользователя по его идентификатору.</summary>
public interface IUserLanguageProvider
{
    Task<string> GetAsync(string userId, CancellationToken cancellationToken = default);
}

public interface IRemotePushProvider
{
    Task<PushDeliveryResult> SendAsync(
        PushDeliveryMessage message,
        CancellationToken cancellationToken = default);
}

public interface IPushNotificationService
{
    Task NotifyInboxMessageAsync(
        string userId,
        PushNotificationType type,
        long inboxMessageId,
        CancellationToken cancellationToken = default);
}
