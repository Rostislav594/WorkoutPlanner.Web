using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GymPlanner.Mobile.Tutorial;

/// <summary>
/// Прожектор обучения: подсветка настоящего элемента страницы и «палец»,
/// который показывает нажатия. Сам сценарий ведут <see cref="TutorialHost"/> и страница.
/// </summary>
/// <remarks>
/// Если модуль не загрузился, обучение всё равно проходит — просто без
/// подсветки, поэтому ошибки JS здесь только записываются в журнал.
/// </remarks>
public sealed class TutorialSpotlight(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task AttachAsync(ElementReference layer)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/todayTutorial.js");
            await _module.InvokeVoidAsync("attach", layer);
        }
        catch (JSException exception)
        {
            System.Diagnostics.Debug.WriteLine($"Tutorial spotlight failed to attach: {exception.Message}");
        }
    }

    /// <summary>
    /// Выводит элемент из-под краевого размытия, докручивает к нему страницу
    /// и ведёт руку к <paramref name="finger"/>; без него рука прячется.
    /// </summary>
    public async Task<bool> FocusAsync(string selector, string? finger)
    {
        if (_module is null)
            return false;

        try
        {
            return await _module.InvokeAsync<bool>("focus", selector, finger);
        }
        catch (JSException exception)
        {
            System.Diagnostics.Debug.WriteLine($"Tutorial focus failed: {exception.Message}");
            return false;
        }
    }

    /// <summary>Ждёт, пока элемент появится на экране, но не дольше <paramref name="timeoutMilliseconds"/>.</summary>
    public async Task<bool> WaitForAsync(string selector, int timeoutMilliseconds)
    {
        if (_module is null)
            return false;

        try
        {
            return await _module.InvokeAsync<bool>("waitFor", selector, timeoutMilliseconds);
        }
        catch (JSException exception)
        {
            System.Diagnostics.Debug.WriteLine($"Tutorial waitFor failed: {exception.Message}");
            return false;
        }
    }

    /// <summary>Обычный клик по настоящему элементу страницы.</summary>
    public Task ClickAsync(string selector) => InvokeLoggedAsync("click", selector);

    public Task HideFingerAsync() => InvokeLoggedAsync("hideFinger");

    public Task TapAsync() => InvokeLoggedAsync("tap");

    public Task PressAsync(bool active) => InvokeLoggedAsync("press", active);

    public Task ClearAsync() => InvokeLoggedAsync("clear");

    public Task DetachAsync() => InvokeLoggedAsync("detach");

    private async Task InvokeLoggedAsync(string identifier, params object?[] arguments)
    {
        if (_module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync(identifier, arguments);
        }
        catch (JSException exception)
        {
            System.Diagnostics.Debug.WriteLine($"Tutorial {identifier} failed: {exception.Message}");
        }
        catch (JSDisconnectedException)
        {
            // WebView уже закрыт вместе со страницей.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync("detach");
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // WebView уже закрыт вместе со страницей.
        }
        catch (JSException exception)
        {
            System.Diagnostics.Debug.WriteLine($"Tutorial spotlight failed to dispose: {exception.Message}");
        }
    }
}
