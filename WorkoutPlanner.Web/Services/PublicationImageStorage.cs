using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Services;

public sealed class PublicationImageStorage(IWebHostEnvironment environment) : IInboxPublicationImageStorage
{
    public const long MaxImageSize = 5 * 1024 * 1024;
    public const string PublicPrefix = "/PublicationImages/";

    public async Task<string> SaveAsync(PhotoUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        await using var input = upload.Content;
        if (upload.Length <= 0 || upload.Length > MaxImageSize)
            throw new InvalidDataException("Изображение должно быть не больше 5 МБ.");

        var extension = upload.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidDataException("Разрешены только JPEG, PNG и WebP.")
        };
        var folder = Path.Combine(environment.ContentRootPath, "App_Data", "PublicationImages");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(folder, fileName);
        try
        {
            await using (var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await CopyWithLimitAsync(input, output, cancellationToken);
            }
            if (!await HasExpectedSignatureAsync(filePath, extension, cancellationToken))
                throw new InvalidDataException("Содержимое файла не соответствует формату изображения.");
            return $"{PublicPrefix}{fileName}";
        }
        catch
        {
            if (File.Exists(filePath)) File.Delete(filePath);
            throw;
        }
    }

    public Task DeleteAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileName = Path.GetFileName(imagePath);
        if (!string.IsNullOrWhiteSpace(fileName) && imagePath == $"{PublicPrefix}{fileName}")
        {
            var path = Path.Combine(environment.ContentRootPath, "App_Data", "PublicationImages", fileName);
            if (File.Exists(path)) File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private static async Task CopyWithLimitAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxImageSize) throw new InvalidDataException("Изображение должно быть не больше 5 МБ.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (total == 0) throw new InvalidDataException("Изображение пустое.");
    }

    private static async Task<bool> HasExpectedSignatureAsync(string path, string extension, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 12, true);
        var count = await stream.ReadAsync(header, cancellationToken);
        return extension switch
        {
            ".jpg" => count >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
            ".png" => count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            ".webp" => count >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
