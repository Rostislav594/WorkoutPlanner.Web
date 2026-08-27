namespace WorkoutPlanner.Web.Services.Activity;

public sealed record UserActivityMetadata(
    string? Platform,
    string? AppVersion,
    string? OsVersion,
    string? DeviceModel)
{
    public const string PlatformHeader = "X-GPlanner-Platform";
    public const string AppVersionHeader = "X-GPlanner-App-Version";
    public const string OsVersionHeader = "X-GPlanner-OS-Version";
    public const string DeviceModelHeader = "X-GPlanner-Device-Model";

    public static UserActivityMetadata Web { get; } =
        new("Web", null, null, null);

    public static UserActivityMetadata FromHeaders(IHeaderDictionary headers) =>
        new(
            headers[PlatformHeader].ToString(),
            headers[AppVersionHeader].ToString(),
            headers[OsVersionHeader].ToString(),
            headers[DeviceModelHeader].ToString());
}
