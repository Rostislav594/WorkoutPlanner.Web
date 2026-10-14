using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Api;

/// <summary>
/// Адреса админки вне Blazor. Страницы админки берут данные напрямую из
/// сервисов, поэтому отдельного API нет — только отдача скриншота обращения
/// для тега &lt;img&gt; на странице обращения.
/// </summary>
public static class AdminOperationsApiEndpoints
{
    public static IEndpointRouteBuilder MapAdminOperationsApi(
        this IEndpointRouteBuilder endpoints)
    {
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
}
