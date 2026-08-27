using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IAdminPublicationService
{
    Task<AdminPublicationItem> CreateAsync(AdminPublicationDraft draft, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminPublicationItem>> GetRecentAsync(int count = 20, CancellationToken cancellationToken = default);
}

public interface IInboxPublicationImageStorage
{
    Task<string> SaveAsync(PhotoUpload upload, CancellationToken cancellationToken = default);
    Task DeleteAsync(string imagePath, CancellationToken cancellationToken = default);
}
