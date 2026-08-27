using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Api;

public static class AdminPublicationApiEndpoints
{
    public sealed record CreateRequest(string Type, string Title, string Body, DateTime PublishedAtUtc);

    public static IEndpointRouteBuilder MapAdminPublicationApi(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/publications")
            .WithTags("Admin Publications")
            .RequireAuthorization(ApplicationRoles.AdminApiPolicy);

        admin.MapGet("", async (IAdminPublicationService service, CancellationToken token) =>
            Results.Ok(await service.GetRecentAsync(cancellationToken: token)));
        admin.MapPost("", async (CreateRequest request, IAdminPublicationService service, CancellationToken token) =>
        {
            if (!Enum.TryParse<InboxMessageType>(request.Type, true, out var type))
                return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Type)] = ["Invalid publication type."] });
            try
            {
                var created = await service.CreateAsync(new AdminPublicationDraft(
                    type, request.Title, request.Body, request.PublishedAtUtc), token);
                return Results.Created($"/api/admin/publications/{created.Id}", created);
            }
            catch (InvalidDataException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["publication"] = [exception.Message] });
            }
        });
        return endpoints;
    }
}
