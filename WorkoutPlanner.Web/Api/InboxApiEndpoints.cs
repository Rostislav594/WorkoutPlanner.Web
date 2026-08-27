using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;

namespace WorkoutPlanner.Web.Api;

public static class InboxApiEndpoints
{
    public static RouteGroupBuilder MapInboxApiEndpoints(this RouteGroupBuilder api)
    {
        var inbox = api.MapGroup("/inbox")
            .WithTags("Inbox")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);

        inbox.MapGet("/messages", GetMessagesAsync)
            .Produces<InboxMessagesResponse>();
        inbox.MapGet("/unread-count", GetUnreadCountAsync)
            .Produces<InboxUnreadCountResponse>();
        inbox.MapPost("/messages/{messageId:long}/read", MarkReadAsync)
            .Produces<InboxUnreadCountResponse>()
            .Produces(StatusCodes.Status404NotFound);
        inbox.MapPost("/publications/{publicationId:long}/read", MarkPublicationReadAsync)
            .Produces<InboxUnreadCountResponse>()
            .Produces(StatusCodes.Status404NotFound);
        inbox.MapDelete("/messages/{messageId:long}", DeleteMessageAsync)
            .Produces<InboxUnreadCountResponse>().Produces(StatusCodes.Status404NotFound);
        inbox.MapDelete("/publications/{publicationId:long}", DeletePublicationAsync)
            .Produces<InboxUnreadCountResponse>().Produces(StatusCodes.Status404NotFound);
        inbox.MapDelete("/messages", DeleteAllAsync)
            .Produces<InboxUnreadCountResponse>();
        inbox.MapPost("/messages/read-all", MarkAllReadAsync)
            .Produces<InboxUnreadCountResponse>();

        return inbox;
    }

    private static async Task<IResult> GetMessagesAsync(
        HttpContext context,
        IInboxService inbox,
        CancellationToken cancellationToken)
    {
        var page = await inbox.GetCurrentAsync(cancellationToken);
        return Results.Ok(new InboxMessagesResponse(
            page.Messages.Select(x => new InboxMessageResponse(
                x.Id,
                x.Type.ToString(),
                x.Title,
                x.Preview,
                x.Body,
                x.CreatedAtUtc,
                x.ReadAtUtc,
                x.SupportTicketNumber,
                x.IsPublication,
                x.ImagePath is null
                    ? null
                    : $"{context.Request.Scheme}://{context.Request.Host}{x.ImagePath}"))
                .ToArray(),
            page.UnreadCount));
    }

    private static async Task<IResult> GetUnreadCountAsync(
        IInboxService inbox,
        CancellationToken cancellationToken) =>
        Results.Ok(new InboxUnreadCountResponse(
            await inbox.GetUnreadCountAsync(cancellationToken)));

    private static async Task<IResult> MarkReadAsync(
        long messageId,
        IInboxService inbox,
        CancellationToken cancellationToken)
    {
        if (!await inbox.MarkReadAsync(messageId, cancellationToken))
            return Results.NotFound();
        return Results.Ok(new InboxUnreadCountResponse(
            await inbox.GetUnreadCountAsync(cancellationToken)));
    }

    private static async Task<IResult> MarkPublicationReadAsync(
        long publicationId,
        IInboxService inbox,
        CancellationToken cancellationToken)
    {
        if (!await inbox.MarkPublicationReadAsync(publicationId, cancellationToken))
            return Results.NotFound();
        return Results.Ok(new InboxUnreadCountResponse(
            await inbox.GetUnreadCountAsync(cancellationToken)));
    }

    private static async Task<IResult> MarkAllReadAsync(
        IInboxService inbox,
        CancellationToken cancellationToken)
    {
        await inbox.MarkAllReadAsync(cancellationToken);
        return Results.Ok(new InboxUnreadCountResponse(0));
    }

    private static async Task<IResult> DeleteMessageAsync(long messageId, IInboxService inbox, CancellationToken cancellationToken)
    {
        if (!await inbox.DeleteMessageAsync(messageId, cancellationToken)) return Results.NotFound();
        return Results.Ok(new InboxUnreadCountResponse(await inbox.GetUnreadCountAsync(cancellationToken)));
    }

    private static async Task<IResult> DeletePublicationAsync(long publicationId, IInboxService inbox, CancellationToken cancellationToken)
    {
        if (!await inbox.DeletePublicationAsync(publicationId, cancellationToken)) return Results.NotFound();
        return Results.Ok(new InboxUnreadCountResponse(await inbox.GetUnreadCountAsync(cancellationToken)));
    }

    private static async Task<IResult> DeleteAllAsync(IInboxService inbox, CancellationToken cancellationToken)
    {
        await inbox.DeleteAllAsync(cancellationToken);
        return Results.Ok(new InboxUnreadCountResponse(await inbox.GetUnreadCountAsync(cancellationToken)));
    }
}
