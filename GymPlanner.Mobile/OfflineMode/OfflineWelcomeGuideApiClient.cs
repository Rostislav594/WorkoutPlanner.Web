using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Состояние приветствия. Шапка спрашивает его при каждом переходе, поэтому без
/// связи ответ берётся из копии, а не ждёт таймаута.
/// </summary>
public sealed class OfflineWelcomeGuideApiClient(WelcomeGuideApiClient inner, OfflineRuntime runtime) : IWelcomeGuideApiClient
{
    public Task<ApiResult<WelcomeGuideStateApiResponse>> GetStateAsync(CancellationToken cancellationToken = default) =>
        runtime.ReadDocumentAsync(OfflineKeys.WelcomeGuide, inner.GetStateAsync, cancellationToken);

    public Task<ApiResult<WelcomeGuideStateApiResponse>> CompleteAsync(CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            inner.CompleteAsync,
            state => runtime.Store.SetAsync(OfflineKeys.WelcomeGuide, state, cancellationToken),
            cancellationToken);
}
