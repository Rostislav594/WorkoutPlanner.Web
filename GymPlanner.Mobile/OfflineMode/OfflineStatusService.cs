using GymPlanner.Mobile.Localization;
using GymPlanner.Mobile.Offline;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>Короткое уведомление под шапкой.</summary>
/// <param name="IsProblem">Что-то не сохранилось — в отличие от простой потери связи.</param>
public sealed record OfflineNotice(string Icon, string Title, string Body, bool IsProblem);

/// <summary>
/// Что показывать в шапке о связи с сервером: значок, короткое уведомление и
/// лист «Офлайн-режим» с очередью изменений и журналом проблем.
/// </summary>
public sealed class OfflineStatusService : IDisposable
{
    private const string LastContactKey = "offline.last-server-contact";
    private static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProblemNoticeDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PendingGrace = TimeSpan.FromSeconds(5);

    private readonly ServerReachability _reachability;
    private readonly ConnectivityMonitor _monitor;
    private readonly OutboxSync _outbox;
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _noticeTimer;
    private bool _wasOffline;

    public OfflineStatusService(
        ServerReachability reachability,
        ConnectivityMonitor monitor,
        OutboxSync outbox,
        TimeProvider timeProvider)
    {
        _reachability = reachability;
        _monitor = monitor;
        _outbox = outbox;
        _timeProvider = timeProvider;
        _wasOffline = reachability.IsOffline;
        _reachability.Changed += ReachabilityChanged;
        _outbox.Changed += OutboxChanged;
        _ = RefreshAsync();
    }

    public event Action? Changed;

    public bool IsOffline => _reachability.IsOffline;

    public bool IsSyncing => _outbox.IsSyncing;

    /// <summary>Изменения, которые ещё не дошли до сервера.</summary>
    public IReadOnlyList<OutboxOperation> Pending { get; private set; } = [];

    /// <summary>Изменения, которые сервер отклонил.</summary>
    public IReadOnlyList<SyncIssue> Issues { get; private set; } = [];

    public bool IsSheetOpen { get; private set; }

    public OfflineNotice? Notice { get; private set; }

    /// <summary>Когда телефон в последний раз получал ответ сервера — в том числе до перезапуска.</summary>
    public DateTimeOffset? LastServerContactUtc
    {
        get
        {
            if (_reachability.LastResponseUtc is { } current)
                return current;

            var stored = Preferences.Default.Get(LastContactKey, 0L);
            return stored > 0 ? new DateTimeOffset(stored, TimeSpan.Zero) : null;
        }
    }

    /// <summary>
    /// Изменения, которые ждут дольше обычной отправки. При связи каждое изменение
    /// на долю секунды попадает в очередь — значок не должен мигать после каждого нажатия.
    /// </summary>
    public int WaitingCount => IsOffline
        ? Pending.Count
        : Pending.Count(x => _timeProvider.GetUtcNow() - x.CreatedAtUtc > PendingGrace);

    /// <summary>Значок нужен только когда что-то не так; при связи и пустой очереди шапка чистая.</summary>
    public bool ShowsIndicator => IsOffline || WaitingCount > 0 || Issues.Count > 0;

    public bool HasProblem => Issues.Count > 0;

    public void OpenSheet()
    {
        IsSheetOpen = true;
        HideNotice();
        Changed?.Invoke();
        _ = RefreshAsync();
    }

    public void CloseSheet()
    {
        IsSheetOpen = false;
        Changed?.Invoke();
    }

    public void CheckConnection() => _monitor.RequestProbe();

    public async Task ClearIssuesAsync()
    {
        await _outbox.ClearIssuesAsync();
        await RefreshAsync();
    }

    public void DismissNotice()
    {
        HideNotice();
        Changed?.Invoke();
    }

    public void ShowIssue(SyncIssue issue) =>
        ShowNotice(
            new OfflineNotice("sync_problem", AppTexts.Get("Offline_IssueNoticeTitle"), issue.Message, IsProblem: true),
            ProblemNoticeDuration);

    private void OutboxChanged() => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            Pending = await _outbox.GetPendingAsync();
            Issues = await _outbox.GetIssuesAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Статус второстепенен: при сбое чтения остаётся прежний.
            System.Diagnostics.Debug.WriteLine($"Offline status refresh failed: {exception.Message}");
        }

        Changed?.Invoke();
    }

    private void ReachabilityChanged()
    {
        var isOffline = _reachability.IsOffline;
        if (!isOffline && _reachability.LastResponseUtc is { } contact)
            Preferences.Default.Set(LastContactKey, contact.UtcTicks);

        if (isOffline && !_wasOffline)
        {
            ShowNotice(
                new OfflineNotice(
                    "cloud_off",
                    AppTexts.Get("Offline_NoticeTitle"),
                    AppTexts.Get("Offline_NoticeBody"),
                    IsProblem: false),
                NoticeDuration);
        }
        else if (!isOffline && Notice is { IsProblem: false })
        {
            HideNotice();
        }

        _wasOffline = isOffline;
        Changed?.Invoke();
    }

    private void ShowNotice(OfflineNotice notice, TimeSpan duration)
    {
        Notice = notice;
        _noticeTimer?.Cancel();
        var timer = new CancellationTokenSource();
        _noticeTimer = timer;
        _ = HideNoticeLaterAsync(duration, timer.Token);
        Changed?.Invoke();
    }

    private async Task HideNoticeLaterAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, _timeProvider, cancellationToken);
            Notice = null;
            Changed?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void HideNotice()
    {
        _noticeTimer?.Cancel();
        _noticeTimer = null;
        Notice = null;
    }

    public void Dispose()
    {
        _reachability.Changed -= ReachabilityChanged;
        _outbox.Changed -= OutboxChanged;
        _noticeTimer?.Cancel();
    }
}
