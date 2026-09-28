using System.Text.Json;
using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Профиль, таймеры отдыха и язык правятся и без связи — через офлайн-очередь.
/// Безопасность аккаунта (сессии, пароль, удаление) всегда идёт напрямую на сервер.
/// </summary>
public sealed class OfflineProfileApiClient(ProfileApiClient inner, OfflineRuntime runtime) : IProfileApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ApiResult<List<AccountSessionResponse>>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
        inner.GetSessionsAsync(cancellationToken);

    public Task<ApiResult> RevokeSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.RevokeSessionAsync(id, cancellationToken);

    public Task<ApiResult<ProfileResponse>> GetAsync(CancellationToken cancellationToken = default) =>
        runtime.ReadDocumentAsync(OfflineKeys.Profile, inner.GetAsync, cancellationToken);

    public async Task<ApiResult<ProfileResponse>> SaveAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var operation = runtime.Outbox.Create(
            OutboxKinds.SaveProfile,
            HttpMethod.Put,
            "api/v1/profile",
            JsonSerializer.Serialize(request, Json));
        return await runtime.SubmitAsync(
            operation,
            async () =>
            {
                var current = await runtime.LoadCopyAsync<ProfileResponse>(OfflineKeys.Profile, cancellationToken)
                    ?? new ProfileResponse(string.Empty, string.Empty, string.Empty, null, string.Empty, false);
                var updated = current with
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    BirthDate = request.BirthDate,
                    Gender = request.Gender,
                    HasProfile = true
                };
                await runtime.UpdateCopyAsync<ProfileResponse>(OfflineKeys.Profile, _ => updated, cancellationToken);
                return updated;
            },
            async body =>
            {
                var profile = JsonSerializer.Deserialize<ProfileResponse>(body, Json)
                    ?? throw new JsonException("Empty profile response.");
                await runtime.UpdateCopyAsync<ProfileResponse>(OfflineKeys.Profile, _ => profile, cancellationToken);
                return profile;
            },
            cancellationToken);
    }

    public async Task<ApiResult<RestTimerSettingsResponse>> SaveRestTimerSettingsAsync(
        UpdateRestTimerSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var operation = runtime.Outbox.Create(
            OutboxKinds.SaveRestTimers,
            HttpMethod.Put,
            "api/v1/profile/rest-timers",
            JsonSerializer.Serialize(request, Json));

        async Task<RestTimerSettingsResponse> ApplyAsync(RestTimerSettingsResponse timers)
        {
            await UpdateProfileAsync(
                x => x with
                {
                    RestBetweenSetsSeconds = timers.RestBetweenSetsSeconds,
                    RestBetweenExercisesSeconds = timers.RestBetweenExercisesSeconds
                },
                cancellationToken);
            return timers;
        }

        return await runtime.SubmitAsync(
            operation,
            () => ApplyAsync(new RestTimerSettingsResponse(request.RestBetweenSetsSeconds, request.RestBetweenExercisesSeconds)),
            body => ApplyAsync(JsonSerializer.Deserialize<RestTimerSettingsResponse>(body, Json)
                ?? throw new JsonException("Empty rest timer response.")),
            cancellationToken);
    }

    public async Task<ApiResult<LanguageSettingsResponse>> SaveLanguageAsync(
        UpdateLanguageRequest request,
        CancellationToken cancellationToken = default)
    {
        var operation = runtime.Outbox.Create(
            OutboxKinds.SaveLanguage,
            HttpMethod.Put,
            "api/v1/profile/language",
            JsonSerializer.Serialize(request, Json));

        async Task<LanguageSettingsResponse> ApplyAsync(LanguageSettingsResponse language)
        {
            await UpdateProfileAsync(x => x with { PreferredLanguage = language.PreferredLanguage }, cancellationToken);
            return language;
        }

        return await runtime.SubmitAsync(
            operation,
            () => ApplyAsync(new LanguageSettingsResponse(request.PreferredLanguage)),
            body => ApplyAsync(JsonSerializer.Deserialize<LanguageSettingsResponse>(body, Json)
                ?? throw new JsonException("Empty language response.")),
            cancellationToken);
    }

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
        runtime.UpdateCopyAsync<ProfileResponse?>(
            OfflineKeys.Profile,
            current => current is null ? null : update(current),
            cancellationToken);
}
