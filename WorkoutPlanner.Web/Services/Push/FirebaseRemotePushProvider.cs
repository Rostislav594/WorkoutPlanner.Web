using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Services.Push;

public sealed class FirebaseRemotePushProvider(
    FirebaseMessaging messaging,
    IPushDeviceStore devices,
    ILogger<FirebaseRemotePushProvider> logger) : IRemotePushProvider
{
    public async Task<PushDeliveryResult> SendAsync(PushDeliveryMessage message, CancellationToken cancellationToken = default)
    {
        var targets = await devices.GetActiveTargetsAsync(message.UserId, cancellationToken);
        if (targets.Count == 0) return PushDeliveryResult.NoRecipients;
        var sent = 0;
        foreach (var target in targets)
        {
            try
            {
                var response = await messaging.SendAsync(new Message
                {
                    Token = target.PushToken,
                    Notification = new Notification { Title = message.Title, Body = message.Body },
                    Data = new Dictionary<string, string>(message.Data, StringComparer.Ordinal),
                    Android = new AndroidConfig { Notification = new AndroidNotification { ChannelId = "gplanner-notifications", Tag = message.InboxMessageId.ToString() } }
                }, cancellationToken);
                sent++;
                logger.LogInformation("FCM push sent for installation {InstallationId}: {MessageId}", target.InstallationId, response);
            }
            catch (FirebaseMessagingException exception) when (exception.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                await devices.DeactivateAsync(target.PushToken, cancellationToken);
                logger.LogInformation("Deactivated unregistered push token for installation {InstallationId}.", target.InstallationId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "FCM push failed for installation {InstallationId}.", target.InstallationId);
            }
        }
        return sent > 0 ? PushDeliveryResult.Sent : PushDeliveryResult.Failed;
    }
}

public static class FirebasePushServiceCollectionExtensions
{
    public static IServiceCollection AddFirebasePush(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(sp => FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.GetApplicationDefault(),
            ProjectId = configuration["Firebase:ProjectId"]
        }));
        services.AddSingleton(sp => FirebaseMessaging.GetMessaging(sp.GetRequiredService<FirebaseApp>()));
        return services;
    }
}
