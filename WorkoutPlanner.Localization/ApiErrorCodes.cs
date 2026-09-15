namespace WorkoutPlanner.Localization;

/// <summary>
/// Стабильные коды ошибок API.
///
/// Контракт: сервер отдаёт КОД, клиент показывает переведённый текст.
/// Русский текст в ответе сервера остаётся как запасной вариант для старых
/// сборок клиента, но новый код должен опираться на код, а не на текст.
///
/// Коды неизменяемы: значение уже выпущенной константы менять нельзя,
/// иначе у пользователей на старых версиях приложения сообщение превратится
/// в сырой код. Устаревшие коды помечаются [Obsolete], но остаются в файле.
///
/// Каждому коду соответствует ключ в ApiErrors.resx с тем же именем.
/// </summary>
public static class ApiErrorCodes
{
    /// <summary>Имя поля в ProblemDetails, в котором приезжает список кодов.</summary>
    public const string ExtensionName = "errorCodes";

    // --- Общие ---
    public const string Unknown = "common.unknown";
    public const string NetworkUnavailable = "common.network_unavailable";
    public const string ServerRejected = "common.server_rejected";
    public const string EmptyResponse = "common.empty_response";
    public const string Unauthorized = "common.unauthorized";
    public const string Forbidden = "common.forbidden";
    public const string NotFound = "common.not_found";

    // --- Профиль ---
    public const string ProfileFirstNameRequired = "profile.first_name_required";
    public const string ProfileLastNameRequired = "profile.last_name_required";
    public const string ProfileBirthDateInvalid = "profile.birth_date_invalid";
    public const string ProfileGenderRequired = "profile.gender_required";
    public const string ProfileLanguageUnsupported = "profile.language_unsupported";
    public const string ProfileSaveFailed = "profile.save_failed";

    // --- Пароль ---
    public const string PasswordCurrentInvalid = "password.current_invalid";
    public const string PasswordTooShort = "password.too_short";
    public const string PasswordsDoNotMatch = "password.mismatch";

    // --- Таймеры отдыха ---
    public const string RestTimerOutOfRange = "rest_timer.out_of_range";

    // --- Обращения в поддержку ---
    public const string SupportMessageTooShort = "support.message_too_short";
    public const string SupportMessageTooLong = "support.message_too_long";
    public const string SupportScreenshotTooLarge = "support.screenshot_too_large";
    public const string SupportScreenshotUnsupported = "support.screenshot_unsupported";

    // --- Тренировки ---
    public const string WorkoutNoExercises = "workout.no_exercises";
    public const string WorkoutInvalidSets = "workout.invalid_sets";
    public const string WorkoutSaveFailed = "workout.save_failed";

    // --- Сопряжение часов ---
    public const string WatchPairingRequestNotFound = "watch_pairing.request_not_found";
    public const string WatchPairingRequestExpired = "watch_pairing.request_expired";
    public const string WatchPairingRequestAlreadyResolved = "watch_pairing.request_already_resolved";
    public const string WatchDeviceAlreadyPaired = "watch_pairing.device_already_paired";
}
