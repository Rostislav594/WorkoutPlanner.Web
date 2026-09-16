using Microsoft.Extensions.Logging;

namespace GymPlanner.Mobile.Diagnostics;

/// <summary>
/// Верхнеуровневый перехват отказов, до которых не добирается ErrorBoundary.
/// </summary>
/// <remarks>
/// Граница ошибок в <c>MainLayout</c> ловит только то, что происходит внутри
/// рендерера Blazor. Отказ фоновой задачи, запущенной как <c>_ = ...Async()</c>,
/// в неё не попадает: у такой задачи никто не ждёт результата, исключение
/// доживает до финализатора и исчезает, не оставив следа. Приложение при этом
/// продолжает работать, но с молча потерянной операцией — например, с
/// остановившимся отсчётом отдыха.
///
/// Здесь эти отказы хотя бы попадают в журнал. Восстановить операцию перехват
/// не может — это задача владельца конкретной задачи.
/// </remarks>
public sealed class UnhandledErrorLogger(ILogger<UnhandledErrorLogger> logger)
{
    private bool _attached;

    public void Attach()
    {
        if (_attached)
            return;

        _attached = true;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args) =>
        logger.LogError(
            args.ExceptionObject as Exception,
            "Unhandled exception outside the Blazor renderer. Terminating: {IsTerminating}.",
            args.IsTerminating);

    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs args)
    {
        // Пометка обязательна: без неё поведение зависит от конфигурации среды
        // выполнения и необработанное исключение задачи может завершить процесс.
        // Потерянная фоновая операция — не повод закрывать приложение у
        // пользователя посреди тренировки.
        args.SetObserved();
        logger.LogError(
            args.Exception,
            "Background task failed with nobody awaiting it; the operation was lost.");
    }
}
