using Microsoft.JSInterop;

namespace GymPlanner.Mobile.Components;

/// <summary>
/// Анимация перестановки суперсета: запомнить позиции до смены порядка
/// и проиграть переезд после отрисовки нового порядка.
/// </summary>
public sealed class SupersetSwapAnimation(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private int? _pendingGroupId;

    /// <summary>Загружает модуль заранее, чтобы первое нажатие не ждало импорта.</summary>
    public async Task PrepareAsync()
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/supersetSwap.js");
        }
        catch (JSException exception)
        {
            System.Diagnostics.Debug.WriteLine($"Superset swap module failed to load: {exception.Message}");
        }
    }

    /// <summary>
    /// Запоминает позиции. Ждать ответа не нужно: сообщения в WebView идут по
    /// порядку, поэтому снимок снимется раньше, чем применится следующая отрисовка.
    /// </summary>
    public Task CaptureAsync(int supersetGroupId)
    {
        if (_module is null)
            return Task.CompletedTask;

        _pendingGroupId = supersetGroupId;
        return InvokeLoggedAsync("capture", supersetGroupId);
    }

    /// <summary>Вызывается из OnAfterRenderAsync, когда новый порядок уже в DOM.</summary>
    public Task PlayPendingAsync()
    {
        if (_pendingGroupId is not { } groupId || _module is null)
            return Task.CompletedTask;

        _pendingGroupId = null;
        return InvokeLoggedAsync("play", groupId);
    }

    private async Task InvokeLoggedAsync(string identifier, int supersetGroupId)
    {
        try
        {
            await _module!.InvokeVoidAsync(identifier, supersetGroupId);
        }
        catch (JSException exception)
        {
            // Без анимации порядок всё равно меняется — просто без переезда.
            System.Diagnostics.Debug.WriteLine($"Superset swap {identifier} failed: {exception.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
            return;

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // WebView уже закрыт вместе со страницей.
        }
    }
}
