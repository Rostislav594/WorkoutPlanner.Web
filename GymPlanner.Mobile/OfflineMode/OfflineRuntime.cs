using System.Text.Json;
using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Authentication;
using GymPlanner.Mobile.Localization;
using GymPlanner.Mobile.Offline;
using Microsoft.Extensions.Logging;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Общая часть офлайн-клиентов API: хранилище текущего аккаунта, состояние связи
/// и правило «сначала сервер, при недоступности — сохранённая копия».
/// </summary>
public sealed class OfflineRuntime
{
    /// <summary>
    /// Сколько экран ждёт ответа на изменение, прежде чем показать «сохранено на
    /// телефоне». Сама отправка на этом не прерывается навсегда: операция остаётся
    /// в очереди и уйдёт следующей попыткой.
    /// </summary>
    private static readonly TimeSpan SubmitWait = TimeSpan.FromSeconds(8);

    private readonly MobileAuthenticationService _authentication;
    private readonly ILogger<OfflineRuntime> _logger;

    public OfflineRuntime(
        OfflineDocumentStore store,
        ServerReachability reachability,
        OutboxSync outbox,
        MobileAuthenticationService authentication,
        ILogger<OfflineRuntime> logger)
    {
        Store = store;
        Reachability = reachability;
        Outbox = outbox;
        _authentication = authentication;
        _logger = logger;
        _authentication.SignedOut += email => _ = ForgetAccountAsync(email);
    }

    public OfflineDocumentStore Store { get; }

    public ServerReachability Reachability { get; }

    public OutboxSync Outbox { get; }

    /// <summary>Просит монитор связи проверить сервер: офлайн-копия отдана без запроса.</summary>
    public event Action? ProbeRequested;

    /// <summary>
    /// Чтение с сервера с сохранением ответа; без связи — сохранённая копия.
    /// </summary>
    public async Task<ApiResult<T>> ReadAsync<T>(
        Func<CancellationToken, Task<ApiResult<T>>> fetch,
        Func<Task<T?>> loadCached,
        Func<T, Task> saveFresh,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!await UseCurrentAccountAsync(cancellationToken))
            return await CallAsync(fetch, cancellationToken);

        if (Reachability.IsOffline && await TryLoadAsync(loadCached) is { } offlineCopy)
        {
            ProbeRequested?.Invoke();
            return ApiResult<T>.Success(offlineCopy);
        }

        // Пока изменения с телефона не дошли до сервера, его ответ их не содержит
        // и затёр бы то, что человек уже видит. Сначала очередь, потом чтение.
        if (await HasUnsentChangesAsync(cancellationToken) && await TryLoadAsync(loadCached) is { } pendingCopy)
            return ApiResult<T>.Success(pendingCopy);

        using var scope = TransportScope.Begin();
        var result = await CallAsync(fetch, cancellationToken);
        if (result.Succeeded)
        {
            await SaveAsync(() => saveFresh(result.Value!));
            return result;
        }

        return scope.ServerUnavailable && await TryLoadAsync(loadCached) is { } cached
            ? ApiResult<T>.Success(cached)
            : result;
    }

    /// <summary>
    /// Изменение через офлайн-очередь. Есть связь — сервер отвечает как обычно;
    /// нет — изменение сразу отражается в копии на телефоне и уходит позже.
    /// </summary>
    /// <param name="projectLocally">Как изменение выглядит, пока сервер его не видел.</param>
    /// <param name="acceptServer">Что сделать с ответом сервера (тело ответа — JSON).</param>
    /// <param name="notFoundIsSuccess">
    /// Для удаления: «уже нет» — тоже нужный исход, например удалили с часов.
    /// </param>
    public async Task<ApiResult<T>> SubmitAsync<T>(
        OutboxOperation operation,
        Func<Task<T>> projectLocally,
        Func<string, Task<T>> acceptServer,
        CancellationToken cancellationToken,
        bool notFoundIsSuccess = false)
    {
        if (!await UseCurrentAccountAsync(cancellationToken))
            return ApiResult<T>.Failure(ApiErrorMessages.NetworkUnavailable());

        var outcome = await Outbox.SubmitAsync(operation, SubmitWait, cancellationToken);
        if (notFoundIsSuccess && outcome is { Kind: OutboxOutcomeKind.Rejected, StatusCode: 404 })
            return ApiResult<T>.Success(await projectLocally());

        try
        {
            return outcome.Kind switch
            {
                OutboxOutcomeKind.Sent => ApiResult<T>.Success(await acceptServer(outcome.Body ?? string.Empty)),
                OutboxOutcomeKind.Queued => ApiResult<T>.Success(await projectLocally()),
                _ => ApiResult<T>.Failure([.. await ReadErrorsAsync(outcome)])
            };
        }
        catch (JsonException)
        {
            // Сервер принял изменение, но ответ не разобрать: копия перечитается при связи.
            return ApiResult<T>.Success(await projectLocally());
        }
    }

    public async Task<ApiResult> SubmitAsync(
        OutboxOperation operation,
        Func<Task> projectLocally,
        CancellationToken cancellationToken,
        bool notFoundIsSuccess = false)
    {
        var result = await SubmitAsync(
            operation,
            async () => { await projectLocally(); return true; },
            async _ => { await projectLocally(); return true; },
            cancellationToken,
            notFoundIsSuccess);
        return result.Succeeded ? ApiResult.Success : new(false, result.Errors);
    }

    public static async Task<IReadOnlyList<string>> ReadErrorsAsync(OutboxOutcome outcome)
    {
        using var response = new HttpResponseMessage((System.Net.HttpStatusCode)outcome.StatusCode)
        {
            Content = new StringContent(outcome.Body ?? string.Empty)
        };
        return await MobileApiErrorReader.ReadAsync(response, CancellationToken.None);
    }

    private async Task<bool> HasUnsentChangesAsync(CancellationToken cancellationToken)
    {
        if (!await Outbox.HasPendingAsync(cancellationToken))
            return false;

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(SubmitWait);
        await Outbox.FlushAsync(limit.Token);
        return await Outbox.HasPendingAsync(cancellationToken);
    }

    /// <summary>Чтение документа целиком по ключу.</summary>
    public Task<ApiResult<T>> ReadDocumentAsync<T>(
        string key,
        Func<CancellationToken, Task<ApiResult<T>>> fetch,
        CancellationToken cancellationToken)
        where T : class =>
        ReadAsync(
            fetch,
            () => Store.GetAsync<T>(key, cancellationToken),
            value => Store.SetAsync(key, value, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Чтение, где «ничего нет» — тоже ответ (сегодня нет тренировки, у упражнения нет фото).
    /// </summary>
    public async Task<OptionalApiResult<T>> ReadOptionalAsync<T>(
        Func<CancellationToken, Task<OptionalApiResult<T>>> fetch,
        Func<Task<(bool Known, T? Value)>> loadCached,
        Func<T?, Task> saveFresh,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!await UseCurrentAccountAsync(cancellationToken))
            return await CallOptionalAsync(fetch, cancellationToken);

        if (Reachability.IsOffline || await HasUnsentChangesAsync(cancellationToken))
        {
            var (known, value) = await TryLoadOptionalAsync(loadCached);
            if (known)
            {
                if (Reachability.IsOffline)
                    ProbeRequested?.Invoke();
                return value is null ? OptionalApiResult<T>.Empty() : OptionalApiResult<T>.Success(value);
            }
        }

        using var scope = TransportScope.Begin();
        var result = await CallOptionalAsync(fetch, cancellationToken);
        if (result.Succeeded)
        {
            await SaveAsync(() => saveFresh(result.Value));
            return result;
        }

        if (scope.ServerUnavailable)
        {
            var (known, value) = await TryLoadOptionalAsync(loadCached);
            if (known)
                return value is null ? OptionalApiResult<T>.Empty() : OptionalApiResult<T>.Success(value);
        }

        return result;
    }

    /// <summary>Запись на сервер; удачный ответ сразу отражается в сохранённой копии.</summary>
    public async Task<ApiResult<T>> WriteAsync<T>(
        Func<CancellationToken, Task<ApiResult<T>>> send,
        Func<T, Task> applyToCopy,
        CancellationToken cancellationToken)
    {
        var result = await CallAsync(send, cancellationToken);
        if (result.Succeeded && await UseCurrentAccountAsync(cancellationToken))
            await SaveAsync(() => applyToCopy(result.Value!));

        return result;
    }

    public async Task<ApiResult> WriteAsync(
        Func<CancellationToken, Task<ApiResult>> send,
        Func<Task> applyToCopy,
        CancellationToken cancellationToken)
    {
        ApiResult result;
        try
        {
            result = await send(cancellationToken);
        }
        catch (JsonException)
        {
            result = ApiResult.Failure(ApiErrorMessages.Unknown());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = ApiResult.Failure(ApiErrorMessages.NetworkUnavailable());
        }

        if (result.Succeeded && await UseCurrentAccountAsync(cancellationToken))
            await SaveAsync(applyToCopy);

        return result;
    }

    /// <summary>Правит сохранённую копию, если за телефоном закреплён аккаунт.</summary>
    public async Task UpdateCopyAsync<T>(string key, Func<T?, T> update, CancellationToken cancellationToken = default)
    {
        if (await UseCurrentAccountAsync(cancellationToken))
            await SaveAsync(() => Store.UpdateAsync(key, update, cancellationToken));
    }

    public async Task<T?> LoadCopyAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!await UseCurrentAccountAsync(cancellationToken))
            return default;

        try
        {
            return await Store.GetAsync<T>(key, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not read offline data.");
            return default;
        }
    }

    public async Task RemoveCopyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (await UseCurrentAccountAsync(cancellationToken))
            await SaveAsync(() => Store.RemoveAsync(key, cancellationToken));
    }

    public async Task ForgetCurrentAccountAsync()
    {
        if (await UseCurrentAccountAsync(CancellationToken.None))
            await Store.DeleteAccountDataAsync();
    }

    private async Task ForgetAccountAsync(string email)
    {
        try
        {
            await Store.UseAccountAsync(email);
            await Store.DeleteAccountDataAsync();
            await Store.UseAccountAsync(_authentication.CurrentEmail);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not delete offline data after sign-out.");
        }
    }

    private async Task<bool> UseCurrentAccountAsync(CancellationToken cancellationToken)
    {
        await Store.UseAccountAsync(_authentication.CurrentEmail, cancellationToken);
        return Store.HasAccount;
    }

    private static async Task<ApiResult<T>> CallAsync<T>(
        Func<CancellationToken, Task<ApiResult<T>>> call,
        CancellationToken cancellationToken)
    {
        try
        {
            return await call(cancellationToken);
        }
        catch (JsonException)
        {
            TransportScope.Current?.MarkServerUnavailable();
            return ApiResult<T>.Failure(ApiErrorMessages.Unknown());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TransportScope.Current?.MarkServerUnavailable();
            return ApiResult<T>.Failure(ApiErrorMessages.NetworkUnavailable());
        }
    }

    private static async Task<OptionalApiResult<T>> CallOptionalAsync<T>(
        Func<CancellationToken, Task<OptionalApiResult<T>>> call,
        CancellationToken cancellationToken)
    {
        try
        {
            return await call(cancellationToken);
        }
        catch (JsonException)
        {
            TransportScope.Current?.MarkServerUnavailable();
            return OptionalApiResult<T>.Failure(ApiErrorMessages.Unknown());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TransportScope.Current?.MarkServerUnavailable();
            return OptionalApiResult<T>.Failure(ApiErrorMessages.NetworkUnavailable());
        }
    }

    private async Task<T?> TryLoadAsync<T>(Func<Task<T?>> load)
        where T : class
    {
        try
        {
            return await load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not read offline data.");
            return null;
        }
    }

    private async Task<(bool Known, T? Value)> TryLoadOptionalAsync<T>(Func<Task<(bool Known, T? Value)>> load)
    {
        try
        {
            return await load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not read offline data.");
            return (false, default);
        }
    }

    // Сбой записи копии не должен ломать ответ сервера, который уже получен.
    private async Task SaveAsync(Func<Task> save)
    {
        try
        {
            await save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not save offline data.");
        }
    }
}
