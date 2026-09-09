using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Services.Support;

public sealed class TelegramSupportNotificationService :
    ISupportNotificationService,
    IDisposable
{
    private readonly TelegramSupportOptions _options;
    private readonly ISupportScreenshotStorage _screenshots;
    private readonly ILogger<TelegramSupportNotificationService> _logger;
    private readonly HttpClient _httpClient;

    public TelegramSupportNotificationService(
        IOptions<TelegramSupportOptions> options,
        ISupportScreenshotStorage screenshots,
        ILogger<TelegramSupportNotificationService> logger)
    {
        _options = options.Value;
        _screenshots = screenshots;
        _logger = logger;
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectCallback = ConnectOverIpv4Async
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<SupportNotificationResult> NotifyTicketCreatedAsync(
        SupportTicketNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken) ||
            string.IsNullOrWhiteSpace(_options.ChatId))
        {
            _logger.LogWarning(
                "Telegram support delivery is disabled for ticket {TicketNumber} because configuration is incomplete.",
                notification.TicketNumber);
            return SupportNotificationResult.NotConfigured;
        }

        try
        {
            var messageId = await SendTextAsync(notification, cancellationToken);
            if (!string.IsNullOrWhiteSpace(notification.ScreenshotPath))
                await SendScreenshotAsync(notification, cancellationToken);

            _logger.LogInformation(
                "Telegram support notification sent for ticket {TicketNumber}.",
                notification.TicketNumber);
            return SupportNotificationResult.Sent(messageId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Telegram support notification timed out for ticket {TicketNumber}.",
                notification.TicketNumber);
            return SupportNotificationResult.Failed;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Telegram support notification failed for ticket {TicketNumber}.",
                notification.TicketNumber);
            return SupportNotificationResult.Failed;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Telegram returned an invalid response for ticket {TicketNumber}.",
                notification.TicketNumber);
            return SupportNotificationResult.Failed;
        }
    }

    private async Task<long?> SendTextAsync(
        SupportTicketNotification notification,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            BuildApiUrl("sendMessage"),
            new
            {
                chat_id = _options.ChatId,
                text = BuildMessage(notification),
                disable_web_page_preview = true
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        if (document.RootElement.TryGetProperty("result", out var result) &&
            result.TryGetProperty("message_id", out var messageId) &&
            messageId.TryGetInt64(out var value))
        {
            return value;
        }

        return null;
    }

    private async Task SendScreenshotAsync(
        SupportTicketNotification notification,
        CancellationToken cancellationToken)
    {
        var screenshot = await _screenshots.OpenAsync(
            notification.ScreenshotPath!,
            cancellationToken);
        if (screenshot is null)
        {
            _logger.LogWarning(
                "Support screenshot was not found for ticket {TicketNumber}.",
                notification.TicketNumber);
            return;
        }

        await using var screenshotStream = screenshot.Content;
        using var file = new StreamContent(screenshotStream);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(screenshot.ContentType);
        using var multipart = new MultipartFormDataContent
        {
            { new StringContent(_options.ChatId), "chat_id" },
            { new StringContent($"Скриншот к обращению {notification.TicketNumber}"), "caption" },
            { file, "photo", GetSafeFileName(screenshot.ContentType) }
        };
        using var response = await _httpClient.PostAsync(
            BuildApiUrl("sendPhoto"),
            multipart,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private string BuildApiUrl(string method) =>
        $"https://api.telegram.org/bot{_options.BotToken}/{method}";

    private static async ValueTask<Stream> ConnectOverIpv4Async(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(
            context.DnsEndPoint.Host,
            AddressFamily.InterNetwork,
            cancellationToken);
        if (addresses.Length == 0)
        {
            throw new HttpRequestException(
                $"No IPv4 address was found for {context.DnsEndPoint.Host}.");
        }

        Exception? lastException = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(
                    address,
                    context.DnsEndPoint.Port,
                    cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (
                exception is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastException = exception;
                if (exception is OperationCanceledException)
                    throw;
            }
        }

        throw new HttpRequestException(
            $"Could not connect to {context.DnsEndPoint.Host} over IPv4.",
            lastException);
    }

    private static string BuildMessage(SupportTicketNotification ticket)
    {
        var builder = new StringBuilder()
            .AppendLine("🆕 GPlanner — новое обращение")
            .AppendLine($"🎫 {ticket.TicketNumber}")
            .AppendLine($"👤 {Fallback(ticket.DisplayName)}")
            .AppendLine($"📧 {Fallback(ticket.Email)}")
            .AppendLine($"🆔 {ticket.UserId}")
            .AppendLine()
            .AppendLine($"📱 {Fallback(ticket.Platform)}")
            .AppendLine($"⚙ {Fallback(ticket.OsVersion)}")
            .AppendLine($"📲 {Fallback(ticket.DeviceModel)}")
            .AppendLine($"📦 GPlanner {Fallback(ticket.AppVersion)}")
            .AppendLine()
            .AppendLine("Описание проблемы:")
            .AppendLine(ticket.Message)
            .AppendLine()
            .Append($"🕐 {ticket.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        return builder.ToString();
    }

    private static string Fallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value;

    private static string GetSafeFileName(string contentType) =>
        contentType switch
        {
            "image/png" => "screenshot.png",
            "image/webp" => "screenshot.webp",
            _ => "screenshot.jpg"
        };

    public void Dispose() => _httpClient.Dispose();
}
