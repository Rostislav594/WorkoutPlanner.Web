namespace GymPlanner.Mobile.Photos;

public enum MobilePhotoSource
{
    Library,
    Camera
}

public sealed record PickedPhoto(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record PhotoPickResult(
    PickedPhoto? Photo,
    bool Cancelled,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Photo is not null;

    public static PhotoPickResult Success(PickedPhoto photo) =>
        new(photo, false, []);

    public static PhotoPickResult Cancel() =>
        new(null, true, []);

    public static PhotoPickResult Failure(params string[] errors) =>
        new(null, false, errors);
}

public interface IMobilePhotoPicker
{
    Task<PhotoPickResult> PickAsync(
        MobilePhotoSource source,
        CancellationToken cancellationToken = default);
}
