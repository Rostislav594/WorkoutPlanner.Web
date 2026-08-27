using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Api;

public static class AdminOperationsApiEndpoints
{
    public sealed record ChangeStatusRequest(string Status);
    public sealed record ReplyRequest(string Message, string? Status);

    public static IEndpointRouteBuilder MapAdminOperationsApi(
        this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(ApplicationRoles.AdminApiPolicy);

        admin.MapGet("/dashboard", async (
            IAdminDashboardService service,
            CancellationToken token) => Results.Ok(await service.GetAsync(token)));

        admin.MapGet("/users", async (
            string? search,
            int? page,
            int? pageSize,
            IAdminUserService service,
            CancellationToken token) => Results.Ok(await service.GetPageAsync(
                new AdminUserListQuery(search, page ?? 1, pageSize ?? 25),
                token)));
        admin.MapGet("/users/{userId}", async (
            string userId,
            IAdminUserService service,
            CancellationToken token) =>
        {
            var user = await service.GetAsync(userId, token);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        admin.MapGet("/support", async (
            string? search,
            string? status,
            int? page,
            int? pageSize,
            IAdminSupportService service,
            CancellationToken token) =>
        {
            if (!TryParseOptionalStatus(status, out var parsedStatus))
                return InvalidStatus();
            var result = await service.GetPageAsync(
                new AdminSupportListQuery(search, parsedStatus, page ?? 1, pageSize ?? 25),
                token);
            return Results.Ok(result);
        });
        admin.MapGet("/support/{ticketId:long}", async (
            long ticketId,
            IAdminSupportService service,
            CancellationToken token) =>
        {
            var ticket = await service.GetAsync(ticketId, token);
            return ticket is null ? Results.NotFound() : Results.Ok(ticket);
        });
        admin.MapPut("/support/{ticketId:long}/status", async (
            long ticketId,
            ChangeStatusRequest request,
            IAdminSupportService service,
            CancellationToken token) =>
        {
            if (!Enum.TryParse<SupportTicketStatus>(request.Status, true, out var status) ||
                !Enum.IsDefined(status))
                return InvalidStatus();
            return await service.ChangeStatusAsync(ticketId, status, token)
                ? Results.NoContent()
                : Results.NotFound();
        });
        admin.MapPost("/support/{ticketId:long}/reply", async (
            long ticketId,
            ReplyRequest request,
            IAdminSupportService service,
            CancellationToken token) =>
        {
            if (!TryParseOptionalStatus(request.Status, out var status))
                return InvalidStatus();
            try
            {
                return await service.ReplyAsync(
                    ticketId,
                    new AdminSupportReply(request.Message, status),
                    token)
                    ? Results.NoContent()
                    : Results.NotFound();
            }
            catch (InvalidDataException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Message)] = [exception.Message]
                });
            }
        });

        admin.MapGet("/system", async (
            IAdminSystemHealthService service,
            CancellationToken token) => Results.Ok(await service.GetAsync(token)));

        endpoints.MapGet("/admin/support/tickets/{ticketId:long}/screenshot", async (
                long ticketId,
                IAdminSupportService service,
                CancellationToken token) =>
            {
                var screenshot = await service.OpenScreenshotAsync(ticketId, token);
                return screenshot is null
                    ? Results.NotFound()
                    : Results.Stream(
                        screenshot.Content,
                        screenshot.ContentType,
                        enableRangeProcessing: true);
            })
            .WithTags("Admin")
            .RequireAuthorization(ApplicationRoles.AdminPolicy);

        return endpoints;
    }

    private static bool TryParseOptionalStatus(
        string? value,
        out SupportTicketStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (!Enum.TryParse<SupportTicketStatus>(value, true, out var parsed) ||
            !Enum.IsDefined(parsed))
            return false;
        status = parsed;
        return true;
    }

    private static IResult InvalidStatus() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["status"] = ["Invalid support ticket status."]
        });
}
