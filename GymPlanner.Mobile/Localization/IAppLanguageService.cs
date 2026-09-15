namespace GymPlanner.Mobile.Localization;

public interface IAppLanguageService
{
    /// <summary>Текущий язык интерфейса: "ru", "uk" или "en".</summary>
    string Current { get; }

    /// <summary>Язык выбран пользователем явно, а не унаследован от устройства.</summary>
    bool IsExplicitlyChosen { get; }

    /// <summary>
    /// Применяет язык к текущему потоку и запоминает его на устройстве.
    /// Возвращает <c>false</c>, если язык не поддерживается.
    /// </summary>
    bool Apply(string languageCode, bool remember = true);

    /// <summary>
    /// Восстанавливает язык при старте: сохранённый выбор, иначе язык устройства,
    /// иначе русский.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Подхватывает язык из профиля, пришедшего с сервера.
    /// Явный локальный выбор при этом не перетирается: пользователь мог
    /// переключить язык на этом устройстве, пока профиль ещё не синхронизировался.
    /// </summary>
    void ApplyFromProfile(string? languageCode);
}
