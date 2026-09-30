using GymPlanner.Mobile.Localization;
using GymPlanner.Mobile.Offline;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>Короткое уведомление под шапкой.</summary>
/// <param name="IsProblem">Что-то не сохранилось — в отличие от простой потери связи.</param>
public sealed record OfflineNotice(string Icon, string Title, string Body, bool IsProblem);

/// <summary>
/// Что показывать в шапке о связи с сервером: значок, короткое уведомление и
/// окно «Нет связи с сервером». Очередь изменений человеку не показываем: она
/// уходит сама. Виден только журнал того, что сервер отклонил.
/// </summary>
public sealed class OfflineStatusService : IDisposable
{
    private static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProblemNoticeDuration = TimeSpan.FromSeconds(8);

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

    /// <summary>Изменения, которые сервер отклонил.</summary>
    public IReadOnlyList<SyncIssue> Issues { get; private set; } = [];

    public bool IsSheetOpen { get; private set; }

    public OfflineNotice? Notice { get; private set; }

    /// <summary>Значок нужен только когда что-то не так: нет связи или изменение не сохранилось.</summary>
    public bool ShowsIndicator => IsOffline || Issues.Count > 0;

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
            Issues = await _outbox.GetIssuesAsync();
            CloseSheetIfNothingToShow();
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
        CloseSheetIfNothingToShow();
        Changed?.Invoke();
    }

    // Связь вернулась и проблем нет — окну «Нет связи» больше нечего сказать.
    private void CloseSheetIfNothingToShow()
    {
        if (IsSheetOpen && !ShowsIndicator)
            IsSheetOpen = false;
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
