namespace GymPlanner.Mobile.ExternalLinks;

public sealed class MauiExternalLinkOpener : IExternalLinkOpener
{
    public async Task<bool> OpenAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("http" or "https"))
            return false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception)
        {
            // No external handler is available: the caller shows a copyable fallback link.
            return false;
        }
    }
}
