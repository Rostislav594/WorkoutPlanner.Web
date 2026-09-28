using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Профиль читается из копии без связи. Безопасность аккаунта (сессии, пароль,
/// удаление) всегда идёт напрямую на сервер.
/// </summary>
public sealed class OfflineProfileApiClient(ProfileApiClient inner, OfflineRuntime runtime) : IProfileApiClient
{
    public Task<ApiResult<List<AccountSessionResponse>>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
        inner.GetSessionsAsync(cancellationToken);

    public Task<ApiResult> RevokeSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.RevokeSessionAsync(id, cancellationToken);

    public Task<ApiResult<ProfileResponse>> GetAsync(CancellationToken cancellationToken = default) =>
        runtime.ReadDocumentAsync(OfflineKeys.Profile, inner.GetAsync, cancellationToken);

    public Task<ApiResult<ProfileResponse>> SaveAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            token => inner.SaveAsync(request, token),
            profile => runtime.Store.SetAsync(OfflineKeys.Profile, profile, cancellationToken),
            cancellationToken);

    public Task<ApiResult<RestTimerSettingsResponse>> SaveRestTimerSettingsAsync(
        UpdateRestTimerSettingsRequest request,
        CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            token => inner.SaveRestTimerSettingsAsync(request, token),
            timers => UpdateProfileAsync(
                x => x with
                {
                    RestBetweenSetsSeconds = timers.RestBetweenSetsSeconds,
                    RestBetweenExercisesSeconds = timers.RestBetweenExercisesSeconds
                },
                cancellationToken),
            cancellationToken);

    public Task<ApiResult<LanguageSettingsResponse>> SaveLanguageAsync(
        UpdateLanguageRequest request,
        CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            token => inner.SaveLanguageAsync(request, token),
            language => UpdateProfileAsync(
                x => x with { PreferredLanguage = language.PreferredLanguage },
                cancellationToken),
            cancellationToken);

    public Task<ApiResult> ChangePasswordAsync(ChangePasswordApiRequest request, CancellationToken cancellationToken = default) =>
        inner.ChangePasswordAsync(request, cancellationToken);

    public async Task<ApiResult> RevokeAccessAsync(CancellationToken cancellationToken = default)
    {
        var result = await inner.RevokeAccessAsync(cancellationToken);
        if (result.Succeeded)
            await runtime.ForgetCurrentAccountAsync();

        return result;
    }

    public async Task<ApiResult> DeleteAccountAsync(CancellationToken cancellationToken = default)
    {
        var result = await inner.DeleteAccountAsync(cancellationToken);
        if (result.Succeeded)
            await runtime.ForgetCurrentAccountAsync();

        return result;
    }

    private Task UpdateProfileAsync(Func<ProfileResponse, ProfileResponse> update, CancellationToken cancellationToken) =>
        runtime.Store.UpdateAsync<ProfileResponse?>(
            OfflineKeys.Profile,
            current => current is null ? null : update(current),
            cancellationToken);
}
