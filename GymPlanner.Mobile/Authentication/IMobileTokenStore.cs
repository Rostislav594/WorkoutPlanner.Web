namespace GymPlanner.Mobile.Authentication;

public interface IMobileTokenStore
{
    Task<MobileTokenSet?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(MobileTokenSet tokens, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
