using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using WorkoutPlanner.Api.Contracts;
using GymPlanner.Mobile.Infrastructure;
using GymPlanner.Mobile.Notifications;

namespace GymPlanner.Mobile.Authentication;

public sealed class MobileAuthenticationService : IDisposable
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);
    private readonly IMobileTokenStore _tokenStore;
    private readonly TimeProvider _timeProvider;
    private readonly HttpClient _authenticationClient;
    private readonly ILocalWorkoutReminderService _reminders;
    private readonly ILogger<MobileAuthenticationService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MobileTokenSet? _tokens;
    private bool _initialized;

    public MobileAuthenticationService(
        MobileApiOptions options,
        IMobileTokenStore tokenStore,
        TimeProvider timeProvider,
        ILocalWorkoutReminderService reminders,
        ILogger<MobileAuthenticationService> logger)
    {
        _tokenStore = tokenStore;
        _timeProvider = timeProvider;
        _reminders = reminders;
        _logger = logger;
        _authenticationClient = new HttpClient
        {
            BaseAddress = options.BaseAddress
        };
    }

    public event Action? AuthenticationChanged;

    public bool IsAuthenticated => _tokens is not null;
    public string? CurrentEmail => _tokens?.Email;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;

            _tokens = await _tokenStore.ReadAsync(cancellationToken);
            _initialized = true;

            if (_tokens is not null && AccessTokenNeedsRefresh(_tokens))
                await TryRefreshCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MobileAuthResult> LoginAsync(
        string email,
        string password,
        string? deviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _authenticationClient.PostAsJsonAsync(
                "api/v1/auth/login",
                new MobileLoginRequest(email.Trim(), password, deviceName),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));

            var tokenResponse = await response.Content.ReadFromJsonAsync<MobileTokenResponse>(
                cancellationToken);
            if (!IsValidTokenResponse(tokenResponse))
            {
                return MobileAuthResult.Failure("Сервер вернул некорректную сессию.");
            }

            var tokens = new MobileTokenSet(
                email.Trim(),
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                _timeProvider.GetUtcNow().AddSeconds(tokenResponse.ExpiresIn));

            await _gate.WaitAsync(cancellationToken);
            try
            {
                await SetTokensCoreAsync(tokens, cancellationToken);
                _initialized = true;
            }
            finally
            {
                _gate.Release();
            }

            AuthenticationChanged?.Invoke();
            return MobileAuthResult.Success;
        }
        catch (HttpRequestException)
        {
            return MobileAuthResult.Failure("Нет соединения с сервером. Проверьте сеть и повторите попытку.");
        }
        catch (JsonException)
        {
            return MobileAuthResult.Failure("Сервер вернул некорректный ответ авторизации.");
        }
    }

    public async Task<MobileAuthResult> RegisterAsync(
        string email,
        string password,
        string? deviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _authenticationClient.PostAsJsonAsync(
                "api/v1/auth/register",
                new MobileRegisterRequest(email.Trim(), password),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return MobileAuthResult.Failure("Нет соединения с сервером. Проверьте сеть и повторите попытку.");
        }

        return await LoginAsync(
            email,
            password,
            deviceName,
            cancellationToken);
    }

    public async Task<string?> GetValidAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_tokens is null)
                return null;

            if (!AccessTokenNeedsRefresh(_tokens))
                return _tokens.AccessToken;

            return await TryRefreshCoreAsync(cancellationToken)
                ? _tokens?.AccessToken
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MobileAuthResult> LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetValidAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            if (!IsAuthenticated)
                return MobileAuthResult.Success;

            return MobileAuthResult.Failure("Не удалось связаться с сервером для завершения сессии.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/logout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _authenticationClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode &&
                response.StatusCode is not HttpStatusCode.Unauthorized and
                    not HttpStatusCode.Forbidden)
            {
                return new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            }
        }
        catch (HttpRequestException)
        {
            return MobileAuthResult.Failure("Нет соединения с сервером. Сессия сохранена для безопасного повтора выхода.");
        }

        await ClearAsync(cancellationToken);
        return MobileAuthResult.Success;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ClearCoreAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }

        AuthenticationChanged?.Invoke();
    }

    private async Task<bool> TryRefreshCoreAsync(CancellationToken cancellationToken)
    {
        if (_tokens is null)
            return false;

        try
        {
            using var response = await _authenticationClient.PostAsJsonAsync(
                "api/v1/auth/refresh",
                new MobileRefreshRequest(_tokens.RefreshToken),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await ClearCoreAsync(cancellationToken);
                AuthenticationChanged?.Invoke();
                return false;
            }

            var refreshed = await response.Content.ReadFromJsonAsync<MobileTokenResponse>(
                cancellationToken);
            if (!IsValidTokenResponse(refreshed))
            {
                await ClearCoreAsync(cancellationToken);
                AuthenticationChanged?.Invoke();
                return false;
            }

            await SetTokensCoreAsync(
                new MobileTokenSet(
                    _tokens.Email,
                    refreshed.AccessToken,
                    refreshed.RefreshToken,
                    _timeProvider.GetUtcNow().AddSeconds(refreshed.ExpiresIn)),
                cancellationToken);
            AuthenticationChanged?.Invoke();
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            await ClearCoreAsync(cancellationToken);
            AuthenticationChanged?.Invoke();
            return false;
        }
    }

    private bool AccessTokenNeedsRefresh(MobileTokenSet tokens) =>
        tokens.AccessTokenExpiresAtUtc <= _timeProvider.GetUtcNow().Add(RefreshMargin);

    private static bool IsValidTokenResponse(
        [NotNullWhen(true)] MobileTokenResponse? response) =>
        response is { ExpiresIn: > 0 } &&
        string.Equals(response.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(response.AccessToken) &&
        !string.IsNullOrWhiteSpace(response.RefreshToken);

    private async Task SetTokensCoreAsync(
        MobileTokenSet tokens,
        CancellationToken cancellationToken)
    {
        await _tokenStore.WriteAsync(tokens, cancellationToken);
        _tokens = tokens;
    }

    private async Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _reminders.CancelAllAsync(cancellationToken);
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Could not cancel all local workout reminders: {Errors}",
                    string.Join(" ", result.Errors));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Could not cancel local workout reminders while clearing the session.");
        }

        await _tokenStore.ClearAsync(cancellationToken);
        _tokens = null;
    }

    public void Dispose()
    {
        _authenticationClient.Dispose();
        _gate.Dispose();
    }
}
