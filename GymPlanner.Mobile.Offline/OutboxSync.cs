using System.Net;
using System.Text;

namespace GymPlanner.Mobile.Offline;

public enum OutboxOutcomeKind
{
    /// <summary>Сервер принял операцию; <see cref="OutboxOutcome.Body"/> — его ответ.</summary>
    Sent,

    /// <summary>Связи нет или сервер временно недоступен: операция ждёт в очереди.</summary>
    Queued,

    /// <summary>Сервер отказал по существу; операция снята с очереди.</summary>
    Rejected
}

public sealed record OutboxOutcome(OutboxOutcomeKind Kind, int StatusCode = 0, string? Body = null);

/// <summary>Что сделать, когда сервер ответил на операцию из очереди.</summary>
public interface IOutboxResultHandler
{
    Task OnSentAsync(OutboxOperation operation, string body);

    Task OnRejectedAsync(OutboxOperation operation, int statusCode, string body);
}

/// <summary>
/// Очередь изменений, сделанных на телефоне, и их отправка на сервер.
/// </summary>
/// <remarks>
/// Операции уходят строго по порядку: следующая ждёт, пока сервер не ответит на
/// предыдущую. Каждая несёт свой ключ повтора, поэтому оборванную на полпути
/// отправку можно смело повторить. Временные сбои (нет сети, 5xx, истёкшая
/// сессия) оставляют операцию в очереди; отказ по существу снимает её и
/// попадает в журнал проблем, который видит человек.
/// </remarks>
public sealed class OutboxSync(
    OfflineDocumentStore store,
    HttpClient client,
    ServerReachability reachability,
    TimeProvider timeProvider)
{
    public const string IdempotencyHeader = "Idempotency-Key";
    private const string OutboxKey = "outbox";
    private const string IssuesKey = "sync-issues";
    private const string IdMapKey = "id-map";
    private const int MaximumIssues = 20;
    private const string RequestInProgressCode = "common.request_in_progress";

    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private int _syncing;

    /// <summary>Назначается приложением: DI не может связать его напрямую без цикла.</summary>
    public IOutboxResultHandler? ResultHandler { get; set; }

    /// <summary>Очередь, её отправка или журнал проблем изменились.</summary>
    public event Action? Changed;

    /// <summary>Сервер обработал операции из очереди (их число): копию стоит перечитать.</summary>
    public event Action<int>? Flushed;

    public bool IsSyncing => Volatile.Read(ref _syncing) == 1;

    public async Task<IReadOnlyList<OutboxOperation>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        await store.GetAsync<List<OutboxOperation>>(OutboxKey, cancellationToken) ?? [];

    public async Task<bool> HasPendingAsync(CancellationToken cancellationToken = default) =>
        (await GetPendingAsync(cancellationToken)).Count > 0;

    public async Task<IReadOnlyList<SyncIssue>> GetIssuesAsync(CancellationToken cancellationToken = default) =>
        await store.GetAsync<List<SyncIssue>>(IssuesKey, cancellationToken) ?? [];

    public async Task AddIssueAsync(SyncIssue issue, CancellationToken cancellationToken = default)
    {
        await store.UpdateAsync<List<SyncIssue>>(
            IssuesKey,
            current => [issue, .. (current ?? []).Take(MaximumIssues - 1)],
            cancellationToken);
        Changed?.Invoke();
    }

    public async Task ClearIssuesAsync(CancellationToken cancellationToken = default)
    {
        await store.RemoveAsync(IssuesKey, cancellationToken);
        Changed?.Invoke();
    }

    public async Task EnqueueAsync(OutboxOperation operation, CancellationToken cancellationToken = default)
    {
        await store.UpdateAsync<List<OutboxOperation>>(
            OutboxKey,
            current => [.. current ?? [], operation],
            cancellationToken);
        Changed?.Invoke();
    }

    /// <summary>
    /// Ставит операцию в очередь и, если сервер доступен, сразу отправляет очередь
    /// до неё включительно. Ждёт не дольше <paramref name="wait"/>: человеку лучше
    /// сразу увидеть «сохранено на телефоне», чем смотреть на бесконечную загрузку.
    /// </summary>
    public async Task<OutboxOutcome> SubmitAsync(
        OutboxOperation operation,
        TimeSpan wait,
        CancellationToken cancellationToken = default)
    {
        await EnqueueAsync(operation, cancellationToken);
        if (reachability.IsOffline)
            return new(OutboxOutcomeKind.Queued);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(wait);
        try
        {
            var (_, outcome) = await FlushCoreAsync(operation.Id, limit.Token);
            return outcome ?? new(OutboxOutcomeKind.Queued);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Не дождались — операция осталась в очереди и уйдёт следующей отправкой.
            return new(OutboxOutcomeKind.Queued);
        }
    }

    /// <summary>Отправляет всю очередь; возвращает, сколько операций сервер обработал.</summary>
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        if (reachability.IsOffline)
            return 0;

        try
        {
            var (processed, _) = await FlushCoreAsync(target: null, cancellationToken);
            return processed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }

    private async Task<(int Processed, OutboxOutcome? Target)> FlushCoreAsync(
        Guid? target,
        CancellationToken cancellationToken)
    {
        await _flushGate.WaitAsync(cancellationToken);
        Volatile.Write(ref _syncing, 1);
        Changed?.Invoke();
        var processed = 0;
        OutboxOutcome? targetOutcome = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var pending = await GetPendingAsync(cancellationToken);
                if (pending.Count == 0)
                    break;

                // Цель уже обработана этой или прошлой отправкой — дальше ждать нечего.
                if (target is { } targetId && pending.All(x => x.Id != targetId) && targetOutcome is null)
                    break;

                var operation = pending[0];
                var map = await GetIdMapAsync(cancellationToken);

                // Родительскую запись сервер создать отказался — эта операция теряет
                // смысл и снимается молча: о самом отказе человек уже узнал.
                if (LocalIdMap.ReferencesRejected(operation, map))
                {
                    await ForgetLocalIdAsync(operation.LocalId);
                    await RemoveAsync(operation.Id);
                    processed++;
                    if (operation.Id == target)
                    {
                        targetOutcome = new(OutboxOutcomeKind.Rejected, 404, string.Empty);
                        break;
                    }

                    continue;
                }

                var outcome = await SendAsync(
                    operation with
                    {
                        Path = LocalIdMap.Rewrite(operation.Path, map)!,
                        Body = LocalIdMap.Rewrite(operation.Body, map)
                    },
                    cancellationToken);
                if (outcome.Kind == OutboxOutcomeKind.Queued)
                    break;

                if (operation.LocalId is { } localId)
                {
                    await RememberIdAsync(
                        localId,
                        outcome.Kind == OutboxOutcomeKind.Sent
                            ? LocalIdMap.ReadCreatedId(operation.Kind, outcome.Body ?? string.Empty)
                            : null);
                }

                if (outcome.Kind == OutboxOutcomeKind.Sent)
                {
                    if (ResultHandler is { } handler)
                        await handler.OnSentAsync(operation, outcome.Body ?? string.Empty);
                }
                else if (ResultHandler is { } handler)
                {
                    await handler.OnRejectedAsync(operation, outcome.StatusCode, outcome.Body ?? string.Empty);
                }

                await RemoveAsync(operation.Id);
                processed++;
                if (operation.Id == target)
                {
                    targetOutcome = outcome;
                    break;
                }
            }
        }
        finally
        {
            Volatile.Write(ref _syncing, 0);
            _flushGate.Release();
            Changed?.Invoke();
        }

        // Ответ на собственную операцию экран применяет сам; перечитывать копию
        // нужно, только если ушли и более ранние изменения из очереди.
        var processedEarlier = processed - (targetOutcome is null ? 0 : 1);
        if (processedEarlier > 0)
            Flushed?.Invoke(processedEarlier);
        return (processed, targetOutcome);
    }

    private async Task<OutboxOutcome> SendAsync(OutboxOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(operation.Method), operation.Path);
            if (operation.Body is not null)
                request.Content = new StringContent(operation.Body, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation(IdempotencyHeader, operation.Id.ToString());

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
                return new(OutboxOutcomeKind.Sent, status, body);

            return IsTemporary(response.StatusCode, body)
                ? new(OutboxOutcomeKind.Queued, status, body)
                : new(OutboxOutcomeKind.Rejected, status, body);
        }
        catch (HttpRequestException)
        {
            return new(OutboxOutcomeKind.Queued);
        }
    }

    private static bool IsTemporary(HttpStatusCode status, string body) =>
        status is HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden or
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests ||
        (int)status >= 500 ||
        status == HttpStatusCode.Conflict && body.Contains(RequestInProgressCode, StringComparison.Ordinal);

    /// <summary>Настоящий ID для временного, если сервер его уже выдал; иначе тот же ID.</summary>
    public async Task<int> ResolveAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!OfflineIds.IsLocal(id))
            return id;

        var map = await GetIdMapAsync(cancellationToken);
        return map.TryGetValue(id, out var real) && real is { } realId ? realId : id;
    }

    private async Task<IReadOnlyDictionary<int, int?>> GetIdMapAsync(CancellationToken cancellationToken) =>
        await store.GetAsync<Dictionary<int, int?>>(IdMapKey, cancellationToken) ?? [];

    private Task RememberIdAsync(int localId, int? realId) =>
        store.UpdateAsync<Dictionary<int, int?>>(
            IdMapKey,
            current =>
            {
                var map = current ?? [];
                if (map.Count >= LocalIdMap.Capacity)
                    map.Remove(map.Keys.First());
                map[localId] = realId;
                return map;
            });

    // Созданное зависимой операцией тоже не появится на сервере.
    private async Task ForgetLocalIdAsync(int? localId)
    {
        if (localId is { } id)
            await RememberIdAsync(id, null);
    }

    // Без токена отмены: ответ сервера уже получен, его нельзя потерять из-за
    // того, что ожидающий экран перестал ждать.
    private async Task RemoveAsync(Guid operationId)
    {
        await store.UpdateAsync<List<OutboxOperation>>(
            OutboxKey,
            current => (current ?? []).Where(x => x.Id != operationId).ToList());
        Changed?.Invoke();
    }

    /// <summary>Новая операция с текущим временем и свежим ключом повтора.</summary>
    public OutboxOperation Create(
        string kind,
        HttpMethod method,
        string path,
        string? body,
        string? label = null,
        int? localId = null) =>
        new(Guid.NewGuid(), kind, method.Method, path, body, timeProvider.GetUtcNow(), label, localId);
}
