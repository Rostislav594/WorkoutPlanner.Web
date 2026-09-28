namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Отмечает, дошёл ли запрос до сервера, в пределах одного вызова API-клиента.
/// </summary>
/// <remarks>
/// Клиенты API отдают ошибки строками и не отличают «сервер недоступен» от
/// «сервер отказал». Офлайн-слой открывает область перед вызовом, а
/// <see cref="ReachabilityHttpHandler"/> отмечает в ней сбой транспорта: так
/// видно, можно ли подставить сохранённые данные вместо ошибки.
/// </remarks>
public sealed class TransportScope : IDisposable
{
    private static readonly AsyncLocal<TransportScope?> CurrentScope = new();
    private readonly TransportScope? _previous;
    private int _serverUnavailable;

    private TransportScope(TransportScope? previous) => _previous = previous;

    public static TransportScope? Current => CurrentScope.Value;

    /// <summary>Запрос не дошёл до сервера, истёк таймаут или сервер ответил 5xx.</summary>
    public bool ServerUnavailable => Volatile.Read(ref _serverUnavailable) == 1;

    public static TransportScope Begin()
    {
        var scope = new TransportScope(CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    public void MarkServerUnavailable() => Volatile.Write(ref _serverUnavailable, 1);

    public void Dispose() => CurrentScope.Value = _previous;
}
