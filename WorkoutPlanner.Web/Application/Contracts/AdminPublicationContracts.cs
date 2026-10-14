using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Application.Contracts;

public sealed record AdminPublicationDraft(
    InboxMessageType Type,
    string Title,
    string Body,
    DateTime PublishedAtUtc,
    PhotoUpload? Image = null,
    bool SendPushNotification = false);

/// <param name="ReadCount">Сколько пользователей открыли публикацию.</param>
/// <param name="AudienceCount">
/// Сколько пользователей её видят: зарегистрированные до момента публикации
/// (то же правило, что во «Входящих» на телефоне).
/// </param>
/// <param name="PushSentAtUtc">Когда разослан push; <c>null</c> — не рассылался.</param>
public sealed record AdminPublicationItem(
    long Id,
    InboxMessageType Type,
    string Title,
    string Body,
    string? ImagePath,
    DateTime PublishedAtUtc,
    DateTime CreatedAtUtc,
    bool SendPushNotification,
    int ReadCount = 0,
    int AudienceCount = 0,
    DateTime? PushSentAtUtc = null);