using Microsoft.AspNetCore.Components.Forms;

namespace WorkoutPlanner.Web.Services;

public sealed class ExercisePhotoService :
    Application.Abstractions.IExercisePhotoService
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
        Application.Contracts.PhotoUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        await using var input = upload.Content;
        if (upload.Length <= 0 || upload.Length > MaxPhotoSize)
            throw new InvalidDataException("The image exceeds the allowed size.");

        var extension = GetTrustedExtension(upload.ContentType);
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
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await CopyWithLimitAsync(input, output, cancellationToken);
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

    public Task<Application.Contracts.PhotoDownload?> OpenPhotoAsync(
        string photoPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileName = Path.GetFileName(photoPath);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(
                photoPath,
                $"/WorkoutImages/{fileName}",
                StringComparison.Ordinal))
        {
            return Task.FromResult<Application.Contracts.PhotoDownload?>(null);
        }

        var uploadPath = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "WorkoutImages",
            fileName);
        var legacyPath = Path.Combine(
            _environment.WebRootPath,
            "WorkoutImages",
            fileName);
        var path = File.Exists(uploadPath) ? uploadPath : legacyPath;
        var contentType = GetContentType(Path.GetExtension(fileName));
        if (contentType is null || !File.Exists(path))
            return Task.FromResult<Application.Contracts.PhotoDownload?>(null);

        try
        {
            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            Application.Contracts.PhotoDownload result = new(
                stream,
                contentType,
                stream.Length);
            return Task.FromResult<Application.Contracts.PhotoDownload?>(result);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Application.Contracts.PhotoDownload?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<Application.Contracts.PhotoDownload?>(null);
        }
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

    private static string? GetContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
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

    private static async Task CopyWithLimitAsync(
        Stream input,
        Stream output,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await input.ReadAsync(
                   buffer,
                   cancellationToken)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > MaxPhotoSize)
            {
                throw new InvalidDataException(
                    "The image exceeds the allowed size.");
            }

            await output.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);
        }

        if (totalBytes == 0)
            throw new InvalidDataException("The image is empty.");
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
