using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Services.Support;
using WorkoutPlanner.Web.Services.Localization;
using WorkoutPlanner.Localization;

namespace WorkoutPlanner.Web.Api;

public static class SupportApiEndpoints
{
    public const string RateLimitPolicyName = "support-tickets";
    private const long MultipartRequestLimit =
        SupportScreenshotStorage.MaxScreenshotSize + (1024 * 1024);

    public static RouteGroupBuilder MapSupportApiEndpoints(
        this RouteGroupBuilder api)
    {
        var support = api.MapGroup("/support/tickets")
            .WithTags("Support")
            .RequireAuthorization(MobileApiAuthorization.PolicyName)
            .RequireRateLimiting(RateLimitPolicyName);

        support.MapPost("", CreateAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MultipartRequestLimit))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<SupportTicketResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status429TooManyRequests);

        return api;
    }

    private static async Task<IResult> CreateAsync(
        HttpRequest request,
        ISupportTicketService tickets,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return ApiProblems.ValidationProblem(
                "message",
                ApiErrorCodes.SupportMessageInvalid,
                ServerTexts.Current["Server_Support_MultipartRequired"]);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var message = form["message"].ToString().Trim();
        if (message.Length < SupportTicketService.MinMessageLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["message"] = [ServerTexts.Current.Format(
                    "Server_Support_MessageTooShort",
                    SupportTicketService.MinMessageLength)]
            });
        }

        if (message.Length > SupportTicketService.MaxMessageLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["message"] = [ServerTexts.Current.Format(
                    "Server_Support_MessageTooLong",
                    SupportTicketService.MaxMessageLength)]
            });
        }

        var file = form.Files.GetFile("screenshot");
        if (file?.Length > SupportScreenshotStorage.MaxScreenshotSize)
        {
            return Results.Problem(
                title: ServerTexts.Current["Server_Support_ScreenshotTooLarge"],
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        PhotoUpload? screenshot = null;
        if (file is { Length: > 0 })
        {
            screenshot = new PhotoUpload(
                file.OpenReadStream(),
                file.ContentType,
                file.Length);
        }

        var result = await tickets.CreateAsync(
            new SupportTicketSubmission(
                message,
                screenshot,
                form["appVersion"].ToString(),
                form["platform"].ToString(),
                form["osVersion"].ToString(),
                form["deviceModel"].ToString()),
            cancellationToken);
        if (result.Succeeded)
        {
            return Results.Created(
                $"/api/v1/support/tickets/{result.TicketNumber}",
                new SupportTicketResponse(
                    result.TicketNumber!,
                    result.CreatedAtUtc!.Value));
        }

        return result.Failure switch
        {
            SupportTicketCreationFailure.InvalidScreenshot =>
                Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["screenshot"] =
                        [ServerTexts.Current["Server_Support_ScreenshotUnsupported"]]
                }),
            _ => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["message"] = [ServerTexts.Current["Server_Support_MessageInvalid"]]
            })
        };
    }
}
