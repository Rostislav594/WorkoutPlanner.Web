namespace GymPlanner.Mobile.ExternalLinks;

public interface IExternalLinkOpener
{
    Task<bool> OpenAsync(string url, CancellationToken cancellationToken = default);
}
