using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IAdminPublicationService
{
    Task<AdminPublicationItem> CreateAsync(AdminPublicationDraft draft, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminPublicationItem>> GetRecentAsync(int count = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Правит публикацию. Новое изображение заменяет старое, <paramref name="removeImage"/>
    /// убирает его. Push, который уже разослан, повторно не уходит.
    /// </summary>
    /// <returns><c>null</c>, если публикации нет.</returns>
    Task<AdminPublicationItem?> UpdateAsync(long id, AdminPublicationDraft draft, bool removeImage = false, CancellationToken cancellationToken = default);

    /// <summary>Удаляет публикацию у всех пользователей вместе с изображением.</summary>
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Сколько устройств получат push о публикации с такой датой.</summary>
    Task<int> CountPushDevicesAsync(DateTime publishedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Шлёт push о вышедшей публикации только на устройства самого админа.
    /// </summary>
    /// <returns>Число устройств; <c>null</c>, если публикации нет или она ещё не вышла.</returns>
    Task<int?> SendTestPushAsync(long id, CancellationToken cancellationToken = default);
}

public interface IInboxPublicationImageStorage
{
    Task<string> SaveAsync(PhotoUpload upload, CancellationToken cancellationToken = default);
    Task DeleteAsync(string imagePath, CancellationToken cancellationToken = default);
}
