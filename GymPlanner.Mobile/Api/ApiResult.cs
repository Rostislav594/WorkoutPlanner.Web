namespace GymPlanner.Mobile.Api;

public sealed record ApiResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static ApiResult Success { get; } = new(true, []);
    public static ApiResult Failure(params string[] errors) => new(false, errors);
}

public sealed record ApiResult<T>(T? Value, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Value is not null;
    public static ApiResult<T> Success(T value) => new(value, []);
    public static ApiResult<T> Failure(params string[] errors) => new(default, errors);
}
