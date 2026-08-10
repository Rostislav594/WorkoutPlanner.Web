namespace GymPlanner.Mobile.Photos;

public sealed class MauiPhotoPicker : IMobilePhotoPicker
{
    private const int MaxPhotoSize = 5 * 1024 * 1024;

    public async Task<PhotoPickResult> PickAsync(
        MobilePhotoSource source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (source == MobilePhotoSource.Camera)
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                    return PhotoPickResult.Failure("Камера недоступна на этом устройстве.");

                var permission = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (permission != PermissionStatus.Granted)
                    permission = await Permissions.RequestAsync<Permissions.Camera>();
                if (permission != PermissionStatus.Granted)
                    return PhotoPickResult.Failure("Разрешите доступ к камере в настройках приложения.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            FileResult? result;
            if (source == MobilePhotoSource.Camera)
            {
                result = await MediaPicker.Default.CapturePhotoAsync();
            }
            else
            {
                var photos = await MediaPicker.Default.PickPhotosAsync(
                    new MediaPickerOptions { SelectionLimit = 1 });
                result = photos.FirstOrDefault();
            }
            if (result is null)
                return PhotoPickResult.Cancel();

            var contentType = GetSupportedContentType(result.ContentType, result.FileName);
            if (contentType is null)
            {
                return PhotoPickResult.Failure(
                    "Поддерживаются только фотографии JPG, PNG и WebP.");
            }

            await using var input = await result.OpenReadAsync();
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                    break;

                if (output.Length + read > MaxPhotoSize)
                    return PhotoPickResult.Failure("Размер фотографии не должен превышать 5 МБ.");

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (output.Length == 0)
                return PhotoPickResult.Failure("Выбранная фотография пуста.");

            var fileName = Path.GetFileName(result.FileName);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = contentType == "image/png" ? "photo.png" : "photo.jpg";

            return PhotoPickResult.Success(
                new PickedPhoto(fileName, contentType, output.ToArray()));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PhotoPickResult.Cancel();
        }
        catch (PermissionException)
        {
            return PhotoPickResult.Failure("Нет разрешения на доступ к фотографии.");
        }
        catch (FeatureNotSupportedException)
        {
            return PhotoPickResult.Failure("Выбор фотографий недоступен на этом устройстве.");
        }
        catch (IOException)
        {
            return PhotoPickResult.Failure("Не удалось прочитать выбранную фотографию.");
        }
    }

    private static string? GetSupportedContentType(
        string? contentType,
        string fileName)
    {
        var normalized = contentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (normalized is "image/jpeg" or "image/png" or "image/webp")
            return normalized;

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
    }
}
