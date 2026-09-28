namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Сохранённый ответ, где «ничего нет» — тоже ответ: отличает известное отсутствие
/// (черновика нет, фото нет) от того, что данные ни разу не загружались.
/// </summary>
public sealed record OfflineOptional<T>(T? Value)
    where T : class;
