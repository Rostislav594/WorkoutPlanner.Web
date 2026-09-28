using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Localization;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Api;

/// <summary>
/// Защита изменяющих запросов телефона от повторов по заголовку <c>Idempotency-Key</c>.
/// </summary>
/// <remarks>
/// Офлайн-очередь не знает, дошёл ли запрос, если ответ потерялся, и отправляет его
/// снова с тем же ключом. Первый ответ запоминается, повтор получает его без
/// выполнения. Ответы 5xx не запоминаются: такой запрос можно честно повторить.
/// Запросы без заголовка (старые сборки, веб) идут как раньше.
/// </remarks>
public sealed class IdempotencyEndpointFilter(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    TimeProvider timeProvider,
    ILogger<IdempotencyEndpointFilter> logger) : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaximumStoredBodyLength = 256_000;

    // Первый запрос мог оборваться вместе с процессом; дольше его не ждём.
    private static readonly TimeSpan InProgressTimeout = TimeSpan.FromMinutes(2);

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var request = http.Request;
        if (HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) ||
            !Guid.TryParse(request.Headers[HeaderName].ToString(), out var key) ||
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { Length: > 0 } userId)
        {
            return await next(context);
        }

        var stored = await ClaimAsync(userId, key, request, http.RequestAborted);
        if (stored is not null)
            return stored;

        var response = http.Response;
        var originalBody = response.Body;
        await using var buffer = new MemoryStream();
        response.Body = buffer;
        try
        {
            var result = await next(context);
            await ExecuteAsync(result, http);
        }
        catch
        {
            response.Body = originalBody;
            await ForgetAsync(userId, key);
            throw;
        }

        response.Body = originalBody;
        var content = buffer.ToArray();
        await RememberAsync(userId, key, response.StatusCode, response.ContentType, content);
        await originalBody.WriteAsync(content, http.RequestAborted);
        return Results.Empty;
    }

    /// <summary>
    /// Занимает ключ. Возвращает готовый ответ, если ключ уже использован,
    /// или <c>null</c>, если запрос надо выполнить.
    /// </summary>
    private async Task<IResult?> ClaimAsync(
        string userId,
        Guid key,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.MobileIdempotencyRecords.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Key == key,
            cancellationToken);
        if (existing is { StatusCode: { } statusCode })
        {
            return Results.Content(
                existing.ResponseBody ?? string.Empty,
                existing.ContentType,
                Encoding.UTF8,
                statusCode);
        }

        if (existing is not null)
        {
            if (now - existing.CreatedAtUtc < InProgressTimeout)
                return InProgress();

            db.MobileIdempotencyRecords.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.MobileIdempotencyRecords.Add(new MobileIdempotencyRecord
        {
            UserId = userId,
            Key = key,
            Method = request.Method,
            Path = Truncate(request.Path.Value ?? string.Empty, 300),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + MobileIdempotencyRecord.RetentionPeriod
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Параллельный запрос с тем же ключом успел раньше.
            return InProgress();
        }

        await db.MobileIdempotencyRecords
            .Where(x => x.UserId == userId && x.ExpiresAtUtc < now)
            .ExecuteDeleteAsync(cancellationToken);
        return null;
    }

    private async Task RememberAsync(
        string userId,
        Guid key,
        int statusCode,
        string? contentType,
        byte[] content)
    {
        if (statusCode >= 500 || content.Length > MaximumStoredBodyLength)
        {
            await ForgetAsync(userId, key);
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await db.MobileIdempotencyRecords
            .Where(x => x.UserId == userId && x.Key == key)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.StatusCode, statusCode)
                .SetProperty(x => x.ContentType, contentType)
                .SetProperty(x => x.ResponseBody, Encoding.UTF8.GetString(content)));
    }

    private async Task ForgetAsync(string userId, Guid key)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.MobileIdempotencyRecords
                .Where(x => x.UserId == userId && x.Key == key && x.StatusCode == null)
                .ExecuteDeleteAsync();
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            // Незавершённая запись сама перестанет мешать через InProgressTimeout.
            logger.LogWarning(exception, "Could not release idempotency key {Key}.", key);
        }
    }

    private static async Task ExecuteAsync(object? result, HttpContext http)
    {
        switch (result)
        {
            case IResult executable:
                await executable.ExecuteAsync(http);
                break;
            case null:
                break;
            default:
                await Results.Ok(result).ExecuteAsync(http);
                break;
        }
    }

    private static IResult InProgress() =>
        ApiProblems.Problem(
            ApiErrorCodes.RequestInProgress,
            "A request with this idempotency key is still in progress.",
            StatusCodes.Status409Conflict);

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
