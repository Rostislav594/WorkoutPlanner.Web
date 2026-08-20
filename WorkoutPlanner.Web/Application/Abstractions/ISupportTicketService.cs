using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface ISupportTicketService
{
    Task<SupportTicketCreationResult> CreateAsync(
        SupportTicketSubmission submission,
        CancellationToken cancellationToken = default);
}

public interface ISupportNotificationService
{
    Task<SupportNotificationResult> NotifyTicketCreatedAsync(
        SupportTicketNotification notification,
        CancellationToken cancellationToken = default);
}

public interface ISupportScreenshotStorage
{
    Task<string> SaveAsync(
        PhotoUpload upload,
        CancellationToken cancellationToken = default);

    Task<PhotoDownload?> OpenAsync(
        string screenshotPath,
        CancellationToken cancellationToken = default);

    bool TryDelete(string? screenshotPath);
}
