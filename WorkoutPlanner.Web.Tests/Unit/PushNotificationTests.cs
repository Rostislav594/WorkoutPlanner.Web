using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Services.Push;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class PushNotificationTests
{
    public static TheoryData<PushNotificationType, string, string> Templates => new()
    {
        {
            PushNotificationType.SupportReply,
            "GPlanner",
            "Вам пришёл ответ от службы поддержки."
        },
        {
            PushNotificationType.InboxMessage,
            "GPlanner",
            "У вас новое сообщение."
        },
        {
            PushNotificationType.AppUpdate,
            "GPlanner",
            "Для вас доступно новое обновление."
        },
        {
            PushNotificationType.AccountNotification,
            "GPlanner",
            "У вас новое уведомление аккаунта."
        }
    };

    public static TheoryData<PushNotificationType, string> PayloadTypes => new()
    {
        { PushNotificationType.SupportReply, "support_reply" },
        { PushNotificationType.InboxMessage, "inbox_message" },
        { PushNotificationType.AppUpdate, "app_update" },
        { PushNotificationType.AccountNotification, "account_notification" }
    };

    [Theory]
    [MemberData(nameof(Templates))]
    public void TemplateProvider_ReturnsExactNeutralTemplate(
        PushNotificationType type,
        string expectedTitle,
        string expectedBody)
    {
        var template = new PushNotificationTemplateProvider().Get(type);

        Assert.Equal(expectedTitle, template.Title);
        Assert.Equal(expectedBody, template.Body);
    }

    [Theory]
    [MemberData(nameof(PayloadTypes))]
    public void TypeSerializer_RoundTripsStablePayloadValue(
        PushNotificationType type,
        string expectedValue)
    {
        Assert.Equal(expectedValue, PushNotificationTypeSerializer.ToPayloadValue(type));
        Assert.True(PushNotificationTypeSerializer.TryParsePayloadValue(expectedValue, out var parsed));
        Assert.Equal(type, parsed);
    }

    [Fact]
    public void TypeSerializer_RejectsUnknownOrDifferentlyCasedValues()
    {
        Assert.False(PushNotificationTypeSerializer.TryParsePayloadValue("SupportReply", out _));
        Assert.False(PushNotificationTypeSerializer.TryParsePayloadValue("private_message", out _));
        Assert.False(PushNotificationTypeSerializer.TryParsePayloadValue(null, out _));
    }

    [Fact]
    public async Task NotifyInboxMessage_UsesAllowlistedPayload_AndNeverLeaksInboxContent()
    {
        const string privacySentinel = "PRIVATE_SENTINEL_user_ticket_body_7f3491";
        var provider = new RecordingPushProvider();
        var service = new PushNotificationService(
            new PushNotificationTemplateProvider(),
            provider,
            NullLogger<PushNotificationService>.Instance);

        await service.NotifyInboxMessageAsync(
            "user-containing-no-private-message-content",
            PushNotificationType.SupportReply,
            1842);

        var message = Assert.Single(provider.Messages);
        Assert.Equal("GPlanner", message.Title);
        Assert.Equal("Вам пришёл ответ от службы поддержки.", message.Body);
        Assert.Equal(
            new[] { PushNotificationPayloadKeys.InboxMessageId, PushNotificationPayloadKeys.Type },
            message.Data.Keys.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal("support_reply", message.Data[PushNotificationPayloadKeys.Type]);
        Assert.Equal("1842", message.Data[PushNotificationPayloadKeys.InboxMessageId]);

        var serializedProviderRequest = JsonSerializer.Serialize(message);
        Assert.DoesNotContain(privacySentinel, serializedProviderRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("ticket_body", serializedProviderRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("\"message\":", serializedProviderRequest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"content\":", serializedProviderRequest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"preview\":", serializedProviderRequest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NotifyInboxMessage_ProviderFailure_DoesNotEscape()
    {
        var service = new PushNotificationService(
            new PushNotificationTemplateProvider(),
            new ThrowingPushProvider(),
            NullLogger<PushNotificationService>.Instance);

        await service.NotifyInboxMessageAsync(
            "user-id",
            PushNotificationType.InboxMessage,
            42);
    }

    private sealed class RecordingPushProvider : IRemotePushProvider
    {
        public List<PushDeliveryMessage> Messages { get; } = [];

        public Task<PushDeliveryResult> SendAsync(
            PushDeliveryMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(message);
            return Task.FromResult(PushDeliveryResult.Sent);
        }
    }

    private sealed class ThrowingPushProvider : IRemotePushProvider
    {
        public Task<PushDeliveryResult> SendAsync(
            PushDeliveryMessage message,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Push provider unavailable.");
    }
}
