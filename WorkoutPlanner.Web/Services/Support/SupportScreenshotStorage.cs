using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Services.Support;

public sealed class SupportScreenshotStorage(
    IWebHostEnvironment environment,
    ILogger<SupportScreenshotStorage> logger) : ISupportScreenshotStorage
{
    public const long MaxScreenshotSize = 5 * 1024 * 1024;
    private const string PublicPrefix = "/SupportScreenshots/";

    public async Task<string> SaveAsync(
        PhotoUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        await using var input = upload.Content;
        if (upload.Length <= 0 || upload.Length > MaxScreenshotSize)
            throw new InvalidDataException("The screenshot exceeds the allowed size.");

        var extension = GetTrustedExtension(upload.ContentType);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var folder = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "SupportScreenshots");
        Directory.CreateDirectory(folder);

        var finalPath = Path.Combine(folder, fileName);
        var temporaryPath = Path.Combine(folder, $"{Guid.NewGuid():N}.upload");
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
                throw new InvalidDataException("Invalid screenshot signature.");
            }

            File.Move(temporaryPath, finalPath);
            return $"{PublicPrefix}{fileName}";
        }
        catch
        {
            TryDeleteFile(temporaryPath);
            throw;
        }
    }

    public Task<PhotoDownload?> OpenAsync(
        string screenshotPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolvePath(screenshotPath, out var physicalPath, out var contentType) ||
            !File.Exists(physicalPath))
        {
            return Task.FromResult<PhotoDownload?>(null);
        }

        try
        {
            var stream = new FileStream(
                physicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult<PhotoDownload?>(
                new PhotoDownload(stream, contentType, stream.Length));
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<PhotoDownload?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<PhotoDownload?>(null);
        }
    }

    public bool TryDelete(string? screenshotPath)
    {
        if (string.IsNullOrWhiteSpace(screenshotPath) ||
            !TryResolvePath(screenshotPath, out var physicalPath, out _))
        {
            return true;
        }

        return TryDeleteFile(physicalPath);
    }

    private bool TryResolvePath(
        string screenshotPath,
        out string physicalPath,
        out string contentType)
    {
        physicalPath = string.Empty;
        contentType = string.Empty;
        var fileName = Path.GetFileName(screenshotPath);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(
                screenshotPath,
                $"{PublicPrefix}{fileName}",
                StringComparison.Ordinal))
        {
            return false;
        }

        contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        physicalPath = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "SupportScreenshots",
            fileName);
        return true;
    }

    private static string GetTrustedExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidDataException(
                "Only JPG, PNG, and WebP screenshots are supported.")
        };

    private static async Task CopyWithLimitAsync(
        Stream input,
        Stream output,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > MaxScreenshotSize)
                throw new InvalidDataException("The screenshot exceeds the allowed size.");

            await output.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);
        }

        if (totalBytes == 0)
            throw new InvalidDataException("The screenshot is empty.");
    }

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
                File.Delete(path);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Could not delete support screenshot {ScreenshotPath}.",
                path);
            return false;
        }
    }
}
