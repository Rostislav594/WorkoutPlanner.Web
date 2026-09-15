namespace WorkoutPlanner.Web.Services.WearOs;

public sealed class WatchPairingOptions
{
    public const string SectionName = "WatchPairing";

    public TimeSpan PairingCodeLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);
    public int MaximumPairingAttempts { get; set; } = 5;

    /// <summary>
    /// Срок жизни заявки на подтверждение с телефона. Намеренно короткий: ссылка
    /// открывается сразу после нажатия на часах, а долгоживущая заявка — это окно
    /// для фишинга «подтвердите чужие часы».
    /// </summary>
    public TimeSpan PairingRequestLifetime { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Шаблон ссылки подтверждения, которую часы открывают на телефоне.
    /// Обязан содержать <c>{requestId}</c>.
    ///
    /// Локально это кастомная схема приложения. В продакшене здесь будет
    /// проверенный App Link вида <c>https://домен/watch/approve?request={requestId}</c>,
    /// который требует размещённого на домене assetlinks.json.
    /// </summary>
    public string ApproveUrlTemplate { get; set; } =
        "gymplanner://watch/approve?request={requestId}";

    public const string RequestIdPlaceholder = "{requestId}";
}
