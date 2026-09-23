using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

/// <summary>
/// Tests del parámetro <c>replyToMessageId</c> soportado por los métodos de envío — verifica que
/// arma el componente <c>context.message_id</c> al nivel superior del payload cuando se informa, y
/// que no aparece en el body cuando no se pasa.
/// </summary>
public class ReplyContextTests
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

    private static (FakeHttpMessageHandler Handler, ArjuyWhatsAppClient Client) BuildSuccessClient(object successResponse)
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(successResponse));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));
        return (handler, client);
    }

    [Fact]
    public async Task SendTextAsync_ConReplyToMessageId_IncluyeContextEnElPayload()
    {
        var (handler, client) = BuildSuccessClient(new { messages = new[] { new { id = "wamid.OUT1" } } });

        var result = await client.SendTextAsync("5491100000000", "Hola", replyToMessageId: "wamid.IN1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequestBody);

        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = doc.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("wamid.IN1", root.GetProperty("context").GetProperty("message_id").GetString());
        // El resto del payload no se pierde al reconstruirlo con el context agregado.
        Assert.Equal("text", root.GetProperty("type").GetString());
        Assert.Equal("Hola", root.GetProperty("text").GetProperty("body").GetString());
    }

    [Fact]
    public async Task SendTextAsync_SinReplyToMessageId_NoIncluyeContextEnElPayload()
    {
        var (handler, client) = BuildSuccessClient(new { messages = new[] { new { id = "wamid.OUT1" } } });

        var result = await client.SendTextAsync("5491100000000", "Hola");

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequestBody);

        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.False(doc.RootElement.TryGetProperty("context", out _));
    }

    [Fact]
    public async Task SendTemplateAsync_OverloadSimple_ConReplyToMessageId_IncluyeContextEnElPayload()
    {
        var (handler, client) = BuildSuccessClient(new { messages = new[] { new { id = "wamid.OUT1" } } });

        var result = await client.SendTemplateAsync(
            "5491100000000", "confirmacion_reserva", "es_AR", new[] { "Juan" }, replyToMessageId: "wamid.IN2");

        Assert.True(result.IsSuccess);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = doc.RootElement;
        Assert.Equal("wamid.IN2", root.GetProperty("context").GetProperty("message_id").GetString());
        Assert.Equal("template", root.GetProperty("type").GetString());
    }

    [Fact]
    public async Task SendTemplateAsync_OverloadConHeaderYBotones_ConReplyToMessageId_IncluyeContextEnElPayload()
    {
        var (handler, client) = BuildSuccessClient(new { messages = new[] { new { id = "wamid.OUT1" } } });

        var headerMedia = new WhatsAppTemplateHeaderMedia { Type = WhatsAppTemplateHeaderMediaType.Image, Link = "https://x.test/logo.png" };

        var result = await client.SendTemplateAsync(
            "5491100000000", "promo", "es_AR", new[] { "Juan" }, headerMedia, replyToMessageId: "wamid.IN3");

        Assert.True(result.IsSuccess);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("wamid.IN3", doc.RootElement.GetProperty("context").GetProperty("message_id").GetString());
    }

    [Fact]
    public async Task SendImageAsync_ConReplyToMessageId_IncluyeContextEnElPayload()
    {
        var (handler, client) = BuildSuccessClient(new { messages = new[] { new { id = "wamid.OUT1" } } });

        var result = await client.SendImageAsync("5491100000000", "https://x.test/img.png", caption: null, replyToMessageId: "wamid.IN4");

        Assert.True(result.IsSuccess);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = doc.RootElement;
        Assert.Equal("wamid.IN4", root.GetProperty("context").GetProperty("message_id").GetString());
        Assert.Equal("image", root.GetProperty("type").GetString());
        Assert.Equal("https://x.test/img.png", root.GetProperty("image").GetProperty("link").GetString());
    }

    [Fact]
    public async Task SendInteractiveButtonsAsync_ConReplyToMessageId_IncluyeContextEnElPayload()
    {
        var (handler, client) = BuildSuccessClient(new { messages = new[] { new { id = "wamid.OUT1" } } });

        var result = await client.SendInteractiveButtonsAsync(
            "5491100000000", "¿Confirmás?", new[] { ("si", "Sí"), ("no", "No") }, replyToMessageId: "wamid.IN5");

        Assert.True(result.IsSuccess);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("wamid.IN5", doc.RootElement.GetProperty("context").GetProperty("message_id").GetString());
    }
}
