using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Services.Support;

namespace WorkoutPlanner.Web.Api;

public static class TelegramSupportWebhookEndpoints
{
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    public static IEndpointRouteBuilder MapTelegramSupportWebhook(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/integrations/telegram/support-webhook", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithTags("Telegram integration")
            .ExcludeFromDescription();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IOptions<TelegramSupportOptions> options,
        IInboxService inbox,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (!HasValidSecret(request, configuration.WebhookSecret))
            return Results.NotFound();

        TelegramWebhookUpdate? update;
        try
        {
            update = await request.ReadFromJsonAsync<TelegramWebhookUpdate>(
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        var message = update?.Message;
        if (message is null ||
            message.Chat.Id.ToString() != configuration.ChatId ||
            message.ReplyToMessage is null ||
            string.IsNullOrWhiteSpace(message.Text))
        {
            return Results.Ok();
        }

        var createdAtUtc = DateTimeOffset.FromUnixTimeSeconds(message.Date)
            .UtcDateTime;
        var stored = await inbox.CreateSupportReplyFromTelegramAsync(
            new TelegramSupportReply(
                message.MessageId,
                message.ReplyToMessage.MessageId,
                message.Text,
                createdAtUtc),
            cancellationToken);
        if (!stored)
        {
            loggerFactory.CreateLogger("TelegramSupportWebhook")
                .LogInformation(
                    "Ignored Telegram reply {TelegramMessageId}: no related support ticket.",
                    message.MessageId);
        }
        return Results.Ok();
    }

    private static bool HasValidSecret(HttpRequest request, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected) ||
            !request.Headers.TryGetValue(SecretHeader, out var received))
            return false;

        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        var receivedBytes = System.Text.Encoding.UTF8.GetBytes(received.ToString());
        return expectedBytes.Length == receivedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
    }

    private sealed record TelegramWebhookUpdate(
        [property: JsonPropertyName("message")] TelegramWebhookMessage? Message);

    private sealed record TelegramWebhookMessage(
        [property: JsonPropertyName("message_id")] long MessageId,
        [property: JsonPropertyName("date")] long Date,
        [property: JsonPropertyName("chat")] TelegramWebhookChat Chat,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("reply_to_message")] TelegramWebhookReply? ReplyToMessage);

    private sealed record TelegramWebhookReply(
        [property: JsonPropertyName("message_id")] long MessageId);

    private sealed record TelegramWebhookChat(
        [property: JsonPropertyName("id")] long Id);
}
