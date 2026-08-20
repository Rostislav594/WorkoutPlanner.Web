using GymPlanner.Mobile.Photos;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed record SupportDeviceContext(
    string? AppVersion,
    string? Platform,
    string? OsVersion,
    string? DeviceModel);

public interface ISupportApiClient
{
    Task<ApiResult<SupportTicketResponse>> CreateAsync(
        string message,
        PickedPhoto? screenshot,
        SupportDeviceContext deviceContext,
        CancellationToken cancellationToken = default);
}
