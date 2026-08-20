namespace WorkoutPlanner.Web.Services.Support;

public sealed class TelegramSupportOptions
{
    public const string SectionName = "TelegramSupport";

    public string BotToken { get; set; } = string.Empty;

    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// Secret configured in Telegram with setWebhook. Webhook processing stays
    /// disabled until it is present.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
