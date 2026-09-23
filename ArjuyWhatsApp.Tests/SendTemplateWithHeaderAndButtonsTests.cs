using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

/// <summary>
/// Tests del overload de <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, WhatsAppTemplateHeaderMedia?, IEnumerable{WhatsAppTemplateButtonParameter}?)"/>
/// que soporta header dinámico de media y botones dinámicos.
/// </summary>
public class SendTemplateWithHeaderAndButtonsTests
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

    private static string SuccessBody(string messageId) => JsonSerializer.Serialize(new
    {
        messaging_product = "whatsapp",
        contacts = new[] { new { input = "5491100000000", wa_id = "5491100000000" } },
        messages = new[] { new { id = messageId } }
    });

    [Fact]
    public async Task OverloadViejo_SinHeaderNiBotones_SigueArmandoSoloBody_ComportamientoRegresion()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody("wamid.SIMPLE"));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendTemplateAsync("5491100000000", "confirmacion_reserva", "es_AR", new[] { "Juan", "12/12" });

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.SIMPLE", result.Data);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var components = bodyDoc.RootElement.GetProperty("template").GetProperty("components");
        Assert.Equal(1, components.GetArrayLength());
        Assert.Equal("body", components[0].GetProperty("type").GetString());
        Assert.Equal("Juan", components[0].GetProperty("parameters")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task HeaderDeImagenPorLink_ArmaComponenteHeaderAntesDelBody()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody("wamid.IMG_HEADER"));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var headerMedia = new WhatsAppTemplateHeaderMedia
        {
            Type = WhatsAppTemplateHeaderMediaType.Image,
            Link = "https://example.com/logo.png"
        };

        var result = await client.SendTemplateAsync("5491100000000", "promo", "es_AR", new[] { "Juan" }, headerMedia);

        Assert.True(result.IsSuccess);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var components = bodyDoc.RootElement.GetProperty("template").GetProperty("components");
        Assert.Equal(2, components.GetArrayLength());

        var header = components[0];
        Assert.Equal("header", header.GetProperty("type").GetString());
        var headerParam = header.GetProperty("parameters")[0];
        Assert.Equal("image", headerParam.GetProperty("type").GetString());
        Assert.Equal("https://example.com/logo.png", headerParam.GetProperty("image").GetProperty("link").GetString());
        Assert.False(headerParam.GetProperty("image").TryGetProperty("id", out _));

        Assert.Equal("body", components[1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task HeaderDeDocumentoPorMediaId_IncluyeFilename()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody("wamid.DOC_HEADER"));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var headerMedia = new WhatsAppTemplateHeaderMedia
        {
            Type = WhatsAppTemplateHeaderMediaType.Document,
            MediaId = "MEDIA_ID_777",
            FileName = "reserva.pdf"
        };

        var result = await client.SendTemplateAsync("5491100000000", "confirmacion_pdf", "es_AR", Array.Empty<string>(), headerMedia);

        Assert.True(result.IsSuccess);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var header = bodyDoc.RootElement.GetProperty("template").GetProperty("components")[0];
        var documentParam = header.GetProperty("parameters")[0].GetProperty("document");
        Assert.Equal("MEDIA_ID_777", documentParam.GetProperty("id").GetString());
        Assert.Equal("reserva.pdf", documentParam.GetProperty("filename").GetString());
        Assert.False(documentParam.TryGetProperty("link", out _));
    }

    [Fact]
    public async Task HeaderDeImagen_NoIncluyeFilename()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody("wamid.IMG_NO_FILENAME"));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var headerMedia = new WhatsAppTemplateHeaderMedia
        {
            Type = WhatsAppTemplateHeaderMediaType.Image,
            Link = "https://example.com/logo.png"
        };

        await client.SendTemplateAsync("5491100000000", "promo", "es_AR", Array.Empty<string>(), headerMedia);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var header = bodyDoc.RootElement.GetProperty("template").GetProperty("components")[0];
        var imageParam = header.GetProperty("parameters")[0].GetProperty("image");
        Assert.False(imageParam.TryGetProperty("filename", out _));
    }

    [Fact]
    public async Task HeaderMedia_ConLinkYMediaIdAmbosSeteados_FallaSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var headerMedia = new WhatsAppTemplateHeaderMedia
        {
            Type = WhatsAppTemplateHeaderMediaType.Image,
            Link = "https://example.com/logo.png",
            MediaId = "MEDIA_ID_777"
        };

        var result = await client.SendTemplateAsync("5491100000000", "promo", "es_AR", Array.Empty<string>(), headerMedia);

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task HeaderMedia_SinLinkNiMediaId_FallaSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var headerMedia = new WhatsAppTemplateHeaderMedia { Type = WhatsAppTemplateHeaderMediaType.Image };

        var result = await client.SendTemplateAsync("5491100000000", "promo", "es_AR", Array.Empty<string>(), headerMedia);

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task BotonUrlDinamico_ArmaComponenteButtonConSubTypeUrlYParametroText()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody("wamid.URL_BUTTON"));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var buttons = new[]
        {
            new WhatsAppTemplateButtonParameter { Index = 0, SubType = WhatsAppTemplateButtonSubType.Url, Value = "pedido-123" }
        };

        var result = await client.SendTemplateAsync("5491100000000", "confirmacion_reserva", "es_AR", new[] { "Juan" }, headerMedia: null, buttonParameters: buttons);

        Assert.True(result.IsSuccess);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var components = bodyDoc.RootElement.GetProperty("template").GetProperty("components");
        Assert.Equal(2, components.GetArrayLength());

        var buttonComponent = components[1];
        Assert.Equal("button", buttonComponent.GetProperty("type").GetString());
        Assert.Equal("url", buttonComponent.GetProperty("sub_type").GetString());
        Assert.Equal(0, buttonComponent.GetProperty("index").GetInt32());
        var buttonParam = buttonComponent.GetProperty("parameters")[0];
        Assert.Equal("text", buttonParam.GetProperty("type").GetString());
        Assert.Equal("pedido-123", buttonParam.GetProperty("text").GetString());
    }

    [Fact]
    public async Task BotonQuickReply_ArmaComponenteButtonConSubTypeQuickReplyYParametroPayload()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody("wamid.QR_BUTTON"));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var buttons = new[]
        {
            new WhatsAppTemplateButtonParameter { Index = 1, SubType = WhatsAppTemplateButtonSubType.QuickReply, Value = "confirmar-turno-456" }
        };

        var result = await client.SendTemplateAsync("5491100000000", "recordatorio_turno", "es_AR", Array.Empty<string>(), headerMedia: null, buttonParameters: buttons);

        Assert.True(result.IsSuccess);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var buttonComponent = bodyDoc.RootElement.GetProperty("template").GetProperty("components")[1];
        Assert.Equal("quick_reply", buttonComponent.GetProperty("sub_type").GetString());
        Assert.Equal(1, buttonComponent.GetProperty("index").GetInt32());
        var buttonParam = buttonComponent.GetProperty("parameters")[0];
        Assert.Equal("payload", buttonParam.GetProperty("type").GetString());
        Assert.Equal("confirmar-turno-456", buttonParam.GetProperty("payload").GetString());
    }

    [Fact]
    public async Task HeaderYBotones_Combinados_QuedanEnOrdenHeaderBodyButtons()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody("wamid.COMBO"));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var headerMedia = new WhatsAppTemplateHeaderMedia
        {
            Type = WhatsAppTemplateHeaderMediaType.Video,
            Link = "https://example.com/promo.mp4"
        };
        var buttons = new[]
        {
            new WhatsAppTemplateButtonParameter { Index = 0, SubType = WhatsAppTemplateButtonSubType.Url, Value = "sku-999" }
        };

        var result = await client.SendTemplateAsync("5491100000000", "promo_video", "es_AR", new[] { "Juan" }, headerMedia, buttons);

        Assert.True(result.IsSuccess);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var components = bodyDoc.RootElement.GetProperty("template").GetProperty("components");
        Assert.Equal(3, components.GetArrayLength());
        Assert.Equal("header", components[0].GetProperty("type").GetString());
        Assert.Equal("video", components[0].GetProperty("parameters")[0].GetProperty("type").GetString());
        Assert.Equal("body", components[1].GetProperty("type").GetString());
        Assert.Equal("button", components[2].GetProperty("type").GetString());
    }
}
