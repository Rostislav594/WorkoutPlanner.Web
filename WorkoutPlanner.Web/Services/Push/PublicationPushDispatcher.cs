using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.Push;

/// <summary>
/// Рассылает push о публикациях из админки. Публикация с отложенной датой
/// уходит, когда эта дата наступила. Перед рассылкой публикация отмечается
/// разосланной, поэтому один и тот же push никогда не уходит дважды.
/// </summary>
public sealed class PublicationPushDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PublicationPushDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);
    private const int PushBodyLength = 160;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await DispatchDueAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Publication push dispatch failed; will retry on the next tick.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Сервер останавливается.
        }
    }

    /// <summary>Рассылает все публикации, чей push пора отправить.</summary>
    public async Task DispatchDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        var provider = scope.ServiceProvider.GetRequiredService<IRemotePushProvider>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var due = await db.InboxPublications
            .Where(x => x.SendPushNotification && x.PushSentAtUtc == null && x.PublishedAtUtc <= now)
            .OrderBy(x => x.PublishedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var publication in due)
        {
            publication.PushSentAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);

            var recipients = await RecipientDevices(db, publication.PublishedAtUtc)
                .Select(device => device.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var userId in recipients)
                await SendAsync(provider, userId, publication, cancellationToken);

            logger.LogInformation(
                "Publication {PublicationId} push sent to {RecipientCount} users.",
                publication.Id,
                recipients.Count);
        }
    }

    /// <summary>
    /// Куда слать: активные устройства тех, кому публикация видна во «Входящих» —
    /// зарегистрированным не позже неё и старым аккаунтам без даты.
    /// </summary>
    internal static IQueryable<PushDeviceRegistration> RecipientDevices(
        WorkoutDbContext db,
        DateTime publishedAtUtc) =>
        db.PushDeviceRegistrations
            .Where(device => device.IsActive)
            .Where(device => !db.UserActivities.Any(activity =>
                activity.UserId == device.UserId &&
                activity.RegisteredAtUtc != null &&
                activity.RegisteredAtUtc > publishedAtUtc));

    /// <summary>Push о публикации: её заголовок и начало текста.</summary>
    internal static PushDeliveryMessage CreateMessage(string userId, InboxPublication publication)
    {
        var preview = publication.Preview ?? publication.Body;
        var body = preview.Length <= PushBodyLength ? preview : $"{preview[..(PushBodyLength - 1)]}…";
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PushNotificationPayloadKeys.Type] =
                PushNotificationTypeSerializer.ToPayloadValue(PushNotificationType.Publication),
            // Телефон ждёт номер в этом поле; для публикации это номер публикации.
            [PushNotificationPayloadKeys.InboxMessageId] =
                publication.Id.ToString(CultureInfo.InvariantCulture)
        };
        return new PushDeliveryMessage(
            userId,
            PushNotificationType.Publication,
            publication.Id,
            publication.Title,
            body,
            data);
    }

    private async Task SendAsync(
        IRemotePushProvider provider,
        string userId,
        InboxPublication publication,
        CancellationToken cancellationToken)
    {
        try
        {
            await provider.SendAsync(CreateMessage(userId, publication), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                          !cancellationToken.IsCancellationRequested)
        {
            // Один пользователь не должен сорвать рассылку остальным.
            logger.LogWarning(
                exception,
                "Publication {PublicationId} push failed for one recipient.",
                publication.Id);
        }
    }
}
