using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

public class AudioVideoStickerTests
{
    private static ArjuyWhatsAppOptions BuildOptions()
    {
        return new ArjuyWhatsAppOptions
        {
            AccessToken = "fake-token",
            PhoneNumberId = "1234567890",
            ApiVersion = "v21.0",
            MaxRetryAttempts = 0
        };
    }

    private static IServiceScopeFactory BuildEmptyScopeFactory()
    {
        return new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static ArjuyWhatsAppClient BuildClient(IHttpClientFactory factory, IOptions<ArjuyWhatsAppOptions> options)
    {
        return new ArjuyWhatsAppClient(
            factory,
            options,
            BuildEmptyScopeFactory(),
            NullLogger<ArjuyWhatsAppClient>.Instance,
            new DefaultPhoneNumberNormalizer());
    }

    private static string SuccessBody(string messageId = "wamid.OUT1") =>
        JsonSerializer.Serialize(new { messages = new[] { new { id = messageId } } });

    // ----------------------- SendAudioAsync / SendAudioByMediaIdAsync -----------------------

    [Fact]
    public async Task SendAudioAsync_ArmaPayloadCorrecto_ConVoiceYReply()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendAudioAsync("5491100000000", "https://example.com/nota.ogg", voice: true, replyToMessageId: "wamid.IN1");

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("audio", root.GetProperty("type").GetString());
        Assert.Equal("https://example.com/nota.ogg", root.GetProperty("audio").GetProperty("link").GetString());
        Assert.True(root.GetProperty("audio").GetProperty("voice").GetBoolean());
        Assert.False(root.GetProperty("audio").TryGetProperty("caption", out _));
        Assert.Equal("wamid.IN1", root.GetProperty("context").GetProperty("message_id").GetString());
    }

    [Fact]
    public async Task SendAudioByMediaIdAsync_ArmaPayloadCorrecto()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendAudioByMediaIdAsync("5491100000000", "media-123");

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var audio = bodyDoc.RootElement.GetProperty("audio");
        Assert.Equal("media-123", audio.GetProperty("id").GetString());
        Assert.False(audio.GetProperty("voice").GetBoolean());
    }

    // ----------------------- SendVideoAsync / SendVideoByMediaIdAsync -----------------------

    [Fact]
    public async Task SendVideoAsync_ArmaPayloadCorrecto_ConCaption()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendVideoAsync("5491100000000", "https://example.com/video.mp4", caption: "Mirá esto");

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var video = bodyDoc.RootElement.GetProperty("video");
        Assert.Equal("https://example.com/video.mp4", video.GetProperty("link").GetString());
        Assert.Equal("Mirá esto", video.GetProperty("caption").GetString());
    }

    [Fact]
    public async Task SendVideoByMediaIdAsync_SinCaption_MandaCaptionVacio()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendVideoByMediaIdAsync("5491100000000", "media-456");

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var video = bodyDoc.RootElement.GetProperty("video");
        Assert.Equal("media-456", video.GetProperty("id").GetString());
        Assert.Equal(string.Empty, video.GetProperty("caption").GetString());
    }

    // ----------------------- SendStickerAsync / SendStickerByMediaIdAsync -----------------------

    [Fact]
    public async Task SendStickerAsync_ArmaPayloadCorrecto_SinCaption()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendStickerAsync("5491100000000", "https://example.com/sticker.webp");

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("sticker", root.GetProperty("type").GetString());
        var sticker = root.GetProperty("sticker");
        Assert.Equal("https://example.com/sticker.webp", sticker.GetProperty("link").GetString());
        Assert.False(sticker.TryGetProperty("caption", out _));
    }

    [Fact]
    public async Task SendStickerByMediaIdAsync_ArmaPayloadCorrecto()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendStickerByMediaIdAsync("5491100000000", "media-789");

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("media-789", bodyDoc.RootElement.GetProperty("sticker").GetProperty("id").GetString());
    }

    // ----------------------- WhatsAppContact.Urls -----------------------

    [Fact]
    public async Task SendContactsAsync_ConUrls_IncluyeArrayUrls()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var contact = new WhatsAppContact
        {
            Name = new WhatsAppContactName { FormattedName = "Juan Pérez" },
            Urls = [new WhatsAppContactUrl { Url = "https://arjuydev.com", Type = "WORK" }]
        };

        var result = await client.SendContactsAsync("5491100000000", [contact]);

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var contactElement = bodyDoc.RootElement.GetProperty("contacts")[0];
        var urls = contactElement.GetProperty("urls");
        Assert.Equal(1, urls.GetArrayLength());
        Assert.Equal("https://arjuydev.com", urls[0].GetProperty("url").GetString());
        Assert.Equal("WORK", urls[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task SendContactsAsync_SinUrls_OmiteCampoUrls()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var contact = new WhatsAppContact { Name = new WhatsAppContactName { FormattedName = "Solo Nombre" } };

        var result = await client.SendContactsAsync("5491100000000", [contact]);

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var contactElement = bodyDoc.RootElement.GetProperty("contacts")[0];
        Assert.False(contactElement.TryGetProperty("urls", out _));
    }
}
