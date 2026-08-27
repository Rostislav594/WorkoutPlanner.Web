using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Support;

namespace WorkoutPlanner.Web.Services.Admin;

public sealed class AdminSystemHealthService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    AdminAccessVerifier access,
    IOptions<TelegramSupportOptions> telegramOptions,
    IWebHostEnvironment environment,
    IEnumerable<IHostedService> hostedServices,
    TimeProvider timeProvider,
    ILogger<AdminSystemHealthService> logger) : IAdminSystemHealthService
{
    public async Task<AdminSystemHealthSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await access.GetRequiredAdminUserIdAsync();
        var components = new List<AdminSystemComponentHealth>
        {
            await GetDatabaseHealthAsync(cancellationToken),
            GetTelegramHealth(),
            GetTelegramWebhookHealth(),
            GetStorageHealth(),
            GetBackgroundServicesHealth(),
            new(
                "Push service",
                AdminSystemHealthStatus.Warning,
                "Push delivery is not configured in the current backend.")
        };
        return new AdminSystemHealthSnapshot(
            components,
            timeProvider.GetUtcNow().UtcDateTime);
    }

    private async Task<AdminSystemComponentHealth> GetDatabaseHealthAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? new("Database", AdminSystemHealthStatus.Healthy, "Database connection is available.")
                : new("Database", AdminSystemHealthStatus.Error, "Database connection is unavailable.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Admin database health check failed.");
            return new("Database", AdminSystemHealthStatus.Error, "Database health check failed.");
        }
    }

    private AdminSystemComponentHealth GetTelegramHealth()
    {
        var options = telegramOptions.Value;
        return !string.IsNullOrWhiteSpace(options.BotToken) &&
               !string.IsNullOrWhiteSpace(options.ChatId)
            ? new(
                "Telegram integration",
                AdminSystemHealthStatus.Healthy,
                "Telegram notification settings are configured.")
            : new(
                "Telegram integration",
                AdminSystemHealthStatus.Warning,
                "Telegram notifications are disabled or incompletely configured.");
    }

    private AdminSystemComponentHealth GetTelegramWebhookHealth() =>
        !string.IsNullOrWhiteSpace(telegramOptions.Value.WebhookSecret)
            ? new(
                "Telegram webhook",
                AdminSystemHealthStatus.Healthy,
                "Webhook request verification is configured.")
            : new(
                "Telegram webhook",
                AdminSystemHealthStatus.Warning,
                "Telegram Reply fallback is disabled because webhook verification is not configured.");

    private AdminSystemComponentHealth GetStorageHealth()
    {
        try
        {
            var appData = Path.Combine(environment.ContentRootPath, "App_Data");
            if (!Directory.Exists(appData))
            {
                return new(
                    "Storage/files",
                    AdminSystemHealthStatus.Warning,
                    "Application data storage has not been initialized yet.");
            }

            var attributes = File.GetAttributes(appData);
            return attributes.HasFlag(FileAttributes.ReadOnly)
                ? new(
                    "Storage/files",
                    AdminSystemHealthStatus.Warning,
                    "Application data storage is marked read-only.")
                : new(
                    "Storage/files",
                    AdminSystemHealthStatus.Healthy,
                    "Application data storage is available.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Admin storage health check failed.");
            return new(
                "Storage/files",
                AdminSystemHealthStatus.Error,
                "Application data storage could not be inspected.");
        }
    }

    private AdminSystemComponentHealth GetBackgroundServicesHealth()
    {
        var count = hostedServices.Count();
        return new(
            "Background services",
            AdminSystemHealthStatus.Healthy,
            count == 0
                ? "No application background services are registered."
                : $"{count} application background service(s) are registered.");
    }
}
