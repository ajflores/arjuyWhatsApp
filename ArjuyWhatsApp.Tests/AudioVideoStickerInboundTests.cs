using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

/// <summary>Tests del parseo de mensajes entrantes de tipo "audio", "video" y "sticker" vía <see cref="ArjuyWhatsAppClient.ProcessWebhookAsync"/>.</summary>
public class AudioVideoStickerInboundTests
{
    private const string AppSecret = "fake-app-secret";

    private static ArjuyWhatsAppClient BuildClient()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var options = new ArjuyWhatsAppOptions { AccessToken = "fake-token", PhoneNumberId = "123", AppSecret = AppSecret, ApiVersion = "v21.0" };

        return new ArjuyWhatsAppClient(
            factory,
            Options.Create(options),
            scopeFactory,
            NullLogger<ArjuyWhatsAppClient>.Instance,
            new DefaultPhoneNumberNormalizer());
    }

    private static string ComputeSignatureHeader(string rawBody, string appSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string WrapMessage(string messageJson)
    {
        return $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [
            {
              "id": "entry-1",
              "changes": [
                {
                  "field": "messages",
                  "value": {
                    "messaging_product": "whatsapp",
                    "messages": [ {{messageJson}} ]
                  }
                }
              ]
            }
          ]
        }
        """;
    }

    [Fact]
    public async Task ProcessWebhookAsync_MensajeAudio_ParseaMediaIdYVoiceNote()
    {
        var messageJson = """
        {
          "from": "5491100000000",
          "id": "wamid.AUDIO1",
          "timestamp": "1700000000",
          "type": "audio",
          "audio": { "id": "media-audio-1", "mime_type": "audio/ogg; codecs=opus", "voice": true }
        }
        """;
        var rawBody = WrapMessage(messageJson);
        var client = BuildClient();

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        var result = await client.ProcessWebhookAsync(rawBody, ComputeSignatureHeader(rawBody, AppSecret));

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal(WhatsAppMessageType.Audio, received!.Type);
        Assert.Equal("media-audio-1", received.MediaId);
        Assert.True(received.IsVoiceNote);
    }

    [Fact]
    public async Task ProcessWebhookAsync_MensajeAudio_SinVoice_QuedaFalse()
    {
        var messageJson = """
        {
          "from": "5491100000000",
          "id": "wamid.AUDIO2",
          "timestamp": "1700000000",
          "type": "audio",
          "audio": { "id": "media-audio-2", "mime_type": "audio/mpeg" }
        }
        """;
        var rawBody = WrapMessage(messageJson);
        var client = BuildClient();

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        await client.ProcessWebhookAsync(rawBody, ComputeSignatureHeader(rawBody, AppSecret));

        Assert.NotNull(received);
        Assert.False(received!.IsVoiceNote);
    }

    [Fact]
    public async Task ProcessWebhookAsync_MensajeVideo_ParseaMediaId()
    {
        var messageJson = """
        {
          "from": "5491100000000",
          "id": "wamid.VIDEO1",
          "timestamp": "1700000000",
          "type": "video",
          "video": { "id": "media-video-1", "mime_type": "video/mp4", "caption": "Mirá esto" }
        }
        """;
        var rawBody = WrapMessage(messageJson);
        var client = BuildClient();

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        var result = await client.ProcessWebhookAsync(rawBody, ComputeSignatureHeader(rawBody, AppSecret));

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal(WhatsAppMessageType.Video, received!.Type);
        Assert.Equal("media-video-1", received.MediaId);
        Assert.Null(received.IsVoiceNote);
    }

    [Fact]
    public async Task ProcessWebhookAsync_MensajeSticker_ParseaMediaId()
    {
        var messageJson = """
        {
          "from": "5491100000000",
          "id": "wamid.STICKER1",
          "timestamp": "1700000000",
          "type": "sticker",
          "sticker": { "id": "media-sticker-1", "mime_type": "image/webp", "animated": false }
        }
        """;
        var rawBody = WrapMessage(messageJson);
        var client = BuildClient();

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        var result = await client.ProcessWebhookAsync(rawBody, ComputeSignatureHeader(rawBody, AppSecret));

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal(WhatsAppMessageType.Sticker, received!.Type);
        Assert.Equal("media-sticker-1", received.MediaId);
    }
}
