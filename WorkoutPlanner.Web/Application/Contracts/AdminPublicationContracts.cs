using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Application.Contracts;

public sealed record AdminPublicationDraft(
    InboxMessageType Type,
    string Title,
    string Body,
    DateTime PublishedAtUtc,
    PhotoUpload? Image = null,
    bool SendPushNotification = false);

public sealed record AdminPublicationItem(
    long Id,
    InboxMessageType Type,
    string Title,
    string Body,
    string? ImagePath,
    DateTime PublishedAtUtc,
    DateTime CreatedAtUtc,
    bool SendPushNotification);
