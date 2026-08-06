using Microsoft.AspNetCore.Mvc;
using WorkoutPlanner.Web.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Services;

namespace WorkoutPlanner.Web.Api;

public static class ExercisePhotoApiEndpoints
{
    private const long MultipartRequestLimit =
        ExercisePhotoService.MaxPhotoSize + (1024 * 1024);

    public static RouteGroupBuilder MapExercisePhotoApiEndpoints(
        this RouteGroupBuilder api)
    {
        var photos = api.MapGroup("/exercises/{exerciseId:int}/photo")
            .WithTags("Exercise photos")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);

        photos.MapPost("", UploadAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MultipartRequestLimit))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ExercisePhotoApiResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        photos.MapGet("", DownloadAsync)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        photos.MapDelete("", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return api;
    }

    private static async Task<IResult> UploadAsync(
        int exerciseId,
        HttpRequest request,
        IExercisePhotoApplicationService photos,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["A multipart image file is required."]
            });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["A non-empty image file is required."]
            });
        }

        if (file.Length > ExercisePhotoService.MaxPhotoSize)
        {
            return Results.Problem(
                title: "The image exceeds the 5 MB limit.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var result = await photos.SaveAsync(
            exerciseId,
            new PhotoUpload(
                file.OpenReadStream(),
                file.ContentType,
                file.Length),
            cancellationToken);
        if (result.Succeeded)
        {
            return Results.Ok(new ExercisePhotoApiResponse(
                exerciseId,
                $"/api/v1/exercises/{exerciseId}/photo"));
        }

        return result.Failure switch
        {
            ExercisePhotoMutationFailure.ExerciseNotFound => Results.NotFound(),
            ExercisePhotoMutationFailure.InvalidFile =>
                Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] =
                        ["Only valid JPG, PNG, and WebP images are supported."]
                }),
            _ => Results.Problem(
                title: "The exercise photo could not be saved.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> DownloadAsync(
        int exerciseId,
        IExercisePhotoApplicationService photos,
        CancellationToken cancellationToken)
    {
        var photo = await photos.OpenAsync(exerciseId, cancellationToken);
        return photo is null
            ? Results.NotFound()
            : Results.Stream(
                photo.Content,
                photo.ContentType,
                enableRangeProcessing: true);
    }

    private static async Task<IResult> DeleteAsync(
        int exerciseId,
        IExercisePhotoApplicationService photos,
        CancellationToken cancellationToken)
    {
        var result = await photos.DeleteAsync(exerciseId, cancellationToken);
        if (result.Succeeded)
            return Results.NoContent();

        return result.Failure == ExercisePhotoMutationFailure.ExerciseNotFound
            ? Results.NotFound()
            : Results.Problem(
                title: "The exercise photo could not be deleted.",
                statusCode: StatusCodes.Status500InternalServerError);
    }
}
