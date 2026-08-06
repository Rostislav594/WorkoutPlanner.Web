using Microsoft.AspNetCore.Components.Forms;

namespace WorkoutPlanner.Web.Services;

public class ExercisePhotoService
{
    public const long MaxPhotoSize = 5 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ExercisePhotoService> _logger;

    public ExercisePhotoService(
        IWebHostEnvironment environment,
        ILogger<ExercisePhotoService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SavePhotoAsync(
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var extension = GetTrustedExtension(file.ContentType);
        var identifier = Guid.NewGuid().ToString("N");
        var fileName = $"{identifier}{extension}";

        var folder = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "WorkoutImages");

        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, fileName);
        var temporaryPath = Path.Combine(folder, $".{identifier}.upload");

        try
        {
            await using (var input = file.OpenReadStream(MaxPhotoSize, cancellationToken))
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            if (!await HasExpectedSignatureAsync(
                    temporaryPath,
                    extension,
                    cancellationToken))
            {
                throw new InvalidDataException("Invalid image file signature.");
            }

            File.Move(temporaryPath, path);
            return $"/WorkoutImages/{fileName}";
        }
        catch
        {
            TryDeleteFile(temporaryPath);
            throw;
        }
    }

    public bool TryDeletePhoto(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
            return true;

        var fileName = Path.GetFileName(photoPath);

        if (string.IsNullOrWhiteSpace(fileName))
            return true;

        var uploadPath = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "WorkoutImages",
            fileName);

        var legacyPath = Path.Combine(
            _environment.WebRootPath,
            "WorkoutImages",
            fileName);

        var uploadDeleted = TryDeleteFile(uploadPath);
        var legacyDeleted = TryDeleteFile(legacyPath);

        return uploadDeleted && legacyDeleted;
    }

    private static string GetTrustedExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidDataException(
                "Only JPG, PNG, and WebP images are supported.")
        };

    private static async Task<bool> HasExpectedSignatureAsync(
        string path,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            header.Length,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var bytesRead = await stream.ReadAsync(header, cancellationToken);

        return extension switch
        {
            ".jpg" => bytesRead >= 3 &&
                      header[0] == 0xFF &&
                      header[1] == 0xD8 &&
                      header[2] == 0xFF,
            ".png" => bytesRead >= 8 &&
                      header.AsSpan(0, 8).SequenceEqual(
                          new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => bytesRead >= 12 &&
                       header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                       header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    private bool TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (IOException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not delete exercise photo {PhotoPath}.",
                path);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Access denied while deleting exercise photo {PhotoPath}.",
                path);
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Unexpected error while deleting exercise photo {PhotoPath}.",
                path);
            return false;
        }
    }
}
