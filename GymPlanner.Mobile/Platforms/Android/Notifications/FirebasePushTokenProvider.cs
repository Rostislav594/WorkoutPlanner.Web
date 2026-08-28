using Firebase.Messaging;
using Android.App;
using Android.Content;
using Java.Lang;

namespace GymPlanner.Mobile.Notifications;

public sealed class FirebasePushTokenProvider : Java.Lang.Object, IRemotePushTokenProvider
{
    public event Action<string>? TokenChanged;

    public System.Threading.Tasks.Task<string?> GetTokenAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        var completion = new System.Threading.Tasks.TaskCompletionSource<string?>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        var task = FirebaseMessaging.Instance.GetToken();
        task.AddOnCompleteListener(new CompletionListener(completion));
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return completion.Task;
    }

    internal void NotifyTokenChanged(string token) => TokenChanged?.Invoke(token);

    private sealed class CompletionListener(System.Threading.Tasks.TaskCompletionSource<string?> completion) : Java.Lang.Object, Android.Gms.Tasks.IOnCompleteListener
    {
        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            if (task.IsSuccessful)
                completion.TrySetResult(task.Result?.ToString());
            else
                completion.TrySetResult(null);
        }
    }
}

[Service(Exported = false, DirectBootAware = true)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public sealed class FirebaseMessagingService : global::Firebase.Messaging.FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        (IPlatformApplication.Current?.Services.GetService<IRemotePushTokenProvider>() as FirebasePushTokenProvider)?.NotifyTokenChanged(token);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        var data = message.Data;
        if (!data.ContainsKey(WorkoutPlanner.Api.Contracts.PushNotificationPayloadKeys.Type) ||
            !data.ContainsKey(WorkoutPlanner.Api.Contracts.PushNotificationPayloadKeys.InboxMessageId)) return;
        AndroidNotificationSupport.ShowRemote(this, message.GetNotification()?.Title ?? "GPlanner", message.GetNotification()?.Body ?? "Новое сообщение", new Dictionary<string, string>(data));
    }
}
