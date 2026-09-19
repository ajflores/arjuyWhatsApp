using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

public class ArjuyWhatsAppClientTests
{
    private static ArjuyWhatsAppOptions BuildOptions()
    {
        return new ArjuyWhatsAppOptions
        {
            AccessToken = "fake-token",
            PhoneNumberId = "1234567890",
            ApiVersion = "v21.0"
        };
    }

    /// <summary>
    /// Scope factory de prueba mínimo, para los tests que no necesitan resolver handlers reales
    /// por DI (los tests de envío de mensajes no disparan ProcessWebhookAsync).
    /// </summary>
    private static IServiceScopeFactory BuildEmptyScopeFactory()
    {
        return new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static ArjuyWhatsAppClient BuildClient(IHttpClientFactory factory, IOptions<ArjuyWhatsAppOptions> options, IServiceScopeFactory? scopeFactory = null)
    {
        return new ArjuyWhatsAppClient(
            factory,
            options,
            scopeFactory ?? BuildEmptyScopeFactory(),
            NullLogger<ArjuyWhatsAppClient>.Instance);
    }

    [Fact]
    public async Task SendTextAsync_RespuestaExitosa_ArmaUrlYHeadersCorrectos_YDevuelveMessageId()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            messaging_product = "whatsapp",
            contacts = new[] { new { input = "5491100000000", wa_id = "5491100000000" } },
            messages = new[] { new { id = "wamid.HBgLNTQ5MTE1NTU1NTU1FQIAERgSMDVFOEQxQ0" } }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var result = await client.SendTextAsync("5491100000000", "Hola desde ArjuyWhatsApp");

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.HBgLNTQ5MTE1NTU1NTU1FQIAERgSMDVFOEQxQ0", result.Data);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://graph.facebook.com/v21.0/1234567890/messages", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("fake-token", handler.LastRequest.Headers.Authorization.Parameter);

        Assert.NotNull(handler.LastRequestBody);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("5491100000000", root.GetProperty("to").GetString());
        Assert.Equal("text", root.GetProperty("type").GetString());
        Assert.Equal("Hola desde ArjuyWhatsApp", root.GetProperty("text").GetProperty("body").GetString());
    }

    [Fact]
    public async Task SendTextAsync_RespuestaDeErrorDeMeta_DevuelveIsSuccessFalseConDetalle()
    {
        var errorBody = JsonSerializer.Serialize(new
        {
            error = new
            {
                message = "Invalid parameter",
                type = "OAuthException",
                code = 100
            }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, errorBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var result = await client.SendTextAsync("5491100000000", "Hola");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Message);
        Assert.Contains("400", result.Message);
        Assert.Contains("Invalid parameter", result.Message);
    }

    [Fact]
    public async Task SendTextAsync_SinAccessToken_DevuelveFailSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(new ArjuyWhatsAppOptions { PhoneNumberId = "123" });
        var client = BuildClient(factory, options);

        var result = await client.SendTextAsync("5491100000000", "Hola");

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task UploadMediaAsync_RespuestaExitosa_ArmaMultipartYDevuelveMediaId()
    {
        var responseBody = JsonSerializer.Serialize(new { id = "MEDIA_ID_123" });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var fileContent = new byte[] { 1, 2, 3, 4 };
        var result = await client.UploadMediaAsync(fileContent, "reserva.pdf", "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("MEDIA_ID_123", result.Data);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://graph.facebook.com/v21.0/1234567890/media", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("fake-token", handler.LastRequest.Headers.Authorization.Parameter);
        Assert.IsType<MultipartFormDataContent>(handler.LastRequest.Content);

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("name=file", handler.LastRequestBody.Replace("\"", string.Empty));
        Assert.Contains("filename=reserva.pdf", handler.LastRequestBody.Replace("\"", string.Empty));
        Assert.Contains("name=type", handler.LastRequestBody.Replace("\"", string.Empty));
        Assert.Contains("application/pdf", handler.LastRequestBody);
        Assert.Contains("name=messaging_product", handler.LastRequestBody.Replace("\"", string.Empty));
        Assert.Contains("whatsapp", handler.LastRequestBody);
    }

    [Fact]
    public async Task UploadMediaAsync_RespuestaDeErrorDeMeta_DevuelveIsSuccessFalseConDetalle()
    {
        var errorBody = JsonSerializer.Serialize(new
        {
            error = new
            {
                message = "Unsupported media type",
                type = "OAuthException",
                code = 100
            }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, errorBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var result = await client.UploadMediaAsync(new byte[] { 1, 2, 3 }, "archivo.pdf", "application/pdf");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Message);
        Assert.Contains("400", result.Message);
        Assert.Contains("Unsupported media type", result.Message);
    }

    [Fact]
    public async Task SendImageByMediaIdAsync_ArmaPayloadConIdEnVezDeLink()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            messaging_product = "whatsapp",
            contacts = new[] { new { input = "5491100000000", wa_id = "5491100000000" } },
            messages = new[] { new { id = "wamid.IMAGE_BY_ID" } }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var result = await client.SendImageByMediaIdAsync("5491100000000", "MEDIA_ID_123", "Mirá esta imagen");

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.IMAGE_BY_ID", result.Data);

        Assert.NotNull(handler.LastRequestBody);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("image", root.GetProperty("type").GetString());
        Assert.Equal("MEDIA_ID_123", root.GetProperty("image").GetProperty("id").GetString());
        Assert.Equal("Mirá esta imagen", root.GetProperty("image").GetProperty("caption").GetString());
        Assert.False(root.GetProperty("image").TryGetProperty("link", out _));
    }

    [Fact]
    public async Task SendDocumentByMediaIdAsync_ArmaPayloadConIdEnVezDeLink()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            messaging_product = "whatsapp",
            contacts = new[] { new { input = "5491100000000", wa_id = "5491100000000" } },
            messages = new[] { new { id = "wamid.DOC_BY_ID" } }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var result = await client.SendDocumentByMediaIdAsync("5491100000000", "MEDIA_ID_456", "reserva.pdf", "Tu reserva");

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.DOC_BY_ID", result.Data);

        Assert.NotNull(handler.LastRequestBody);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("document", root.GetProperty("type").GetString());
        Assert.Equal("MEDIA_ID_456", root.GetProperty("document").GetProperty("id").GetString());
        Assert.Equal("reserva.pdf", root.GetProperty("document").GetProperty("filename").GetString());
        Assert.Equal("Tu reserva", root.GetProperty("document").GetProperty("caption").GetString());
        Assert.False(root.GetProperty("document").TryGetProperty("link", out _));
    }

    [Fact]
    public async Task SendInteractiveButtonsAsync_ArmaPayloadConEstructuraEsperada()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            messaging_product = "whatsapp",
            contacts = new[] { new { input = "5491100000000", wa_id = "5491100000000" } },
            messages = new[] { new { id = "wamid.BUTTONS1" } }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var buttons = new[]
        {
            ("op1", "Confirmar"),
            ("op2", "Cancelar")
        };

        var result = await client.SendInteractiveButtonsAsync("5491100000000", "¿Confirmás tu reserva?", buttons);

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.BUTTONS1", result.Data);

        Assert.NotNull(handler.LastRequestBody);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("interactive", root.GetProperty("type").GetString());
        var interactive = root.GetProperty("interactive");
        Assert.Equal("button", interactive.GetProperty("type").GetString());
        Assert.Equal("¿Confirmás tu reserva?", interactive.GetProperty("body").GetProperty("text").GetString());

        var buttonsArray = interactive.GetProperty("action").GetProperty("buttons");
        Assert.Equal(2, buttonsArray.GetArrayLength());
        Assert.Equal("reply", buttonsArray[0].GetProperty("type").GetString());
        Assert.Equal("op1", buttonsArray[0].GetProperty("reply").GetProperty("id").GetString());
        Assert.Equal("Confirmar", buttonsArray[0].GetProperty("reply").GetProperty("title").GetString());
        Assert.Equal("op2", buttonsArray[1].GetProperty("reply").GetProperty("id").GetString());
        Assert.Equal("Cancelar", buttonsArray[1].GetProperty("reply").GetProperty("title").GetString());
    }

    [Fact]
    public async Task SendInteractiveButtonsAsync_MasDeTresBotones_FallaSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var buttons = new[]
        {
            ("op1", "Uno"),
            ("op2", "Dos"),
            ("op3", "Tres"),
            ("op4", "Cuatro")
        };

        var result = await client.SendInteractiveButtonsAsync("5491100000000", "Elegí una opción", buttons);

        Assert.False(result.IsSuccess);
        Assert.Contains("3", result.Message);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendInteractiveButtonsAsync_TituloDeMasDeVeinteCaracteres_FallaSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var buttons = new[] { ("op1", "Este título tiene más de veinte caracteres") };

        var result = await client.SendInteractiveButtonsAsync("5491100000000", "Elegí una opción", buttons);

        Assert.False(result.IsSuccess);
        Assert.Contains("20", result.Message);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendInteractiveButtonsAsync_TitulosDuplicados_FallaSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var buttons = new[] { ("op1", "Sí"), ("op2", "Sí") };

        var result = await client.SendInteractiveButtonsAsync("5491100000000", "Elegí una opción", buttons);

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendInteractiveListAsync_ArmaPayloadConEstructuraEsperada()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            messaging_product = "whatsapp",
            contacts = new[] { new { input = "5491100000000", wa_id = "5491100000000" } },
            messages = new[] { new { id = "wamid.LIST1" } }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var sections = new[]
        {
            ("Excursiones", (IEnumerable<(string Id, string Title, string? Description)>)new[]
            {
                ("exc1", "Full day glaciar", (string?)"Salida 7am, incluye almuerzo"),
                ("exc2", "City tour", (string?)null)
            })
        };

        var result = await client.SendInteractiveListAsync("5491100000000", "Elegí tu excursión", "Ver opciones", sections);

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.LIST1", result.Data);

        Assert.NotNull(handler.LastRequestBody);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("interactive", root.GetProperty("type").GetString());
        var interactive = root.GetProperty("interactive");
        Assert.Equal("list", interactive.GetProperty("type").GetString());
        Assert.Equal("Elegí tu excursión", interactive.GetProperty("body").GetProperty("text").GetString());

        var action = interactive.GetProperty("action");
        Assert.Equal("Ver opciones", action.GetProperty("button").GetString());

        var sectionsArray = action.GetProperty("sections");
        Assert.Equal(1, sectionsArray.GetArrayLength());
        Assert.Equal("Excursiones", sectionsArray[0].GetProperty("title").GetString());

        var rows = sectionsArray[0].GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("exc1", rows[0].GetProperty("id").GetString());
        Assert.Equal("Full day glaciar", rows[0].GetProperty("title").GetString());
        Assert.Equal("Salida 7am, incluye almuerzo", rows[0].GetProperty("description").GetString());
        Assert.Equal("exc2", rows[1].GetProperty("id").GetString());
        Assert.Equal("City tour", rows[1].GetProperty("title").GetString());
    }

    [Fact]
    public async Task SendInteractiveListAsync_MasDeDiezFilasEnTotal_FallaSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var rows = Enumerable.Range(1, 11)
            .Select(i => (Id: $"op{i}", Title: $"Opción {i}", Description: (string?)null))
            .ToArray();

        var sections = new[]
        {
            ("Sección única", (IEnumerable<(string Id, string Title, string? Description)>)rows)
        };

        var result = await client.SendInteractiveListAsync("5491100000000", "Elegí una opción", "Ver opciones", sections);

        Assert.False(result.IsSuccess);
        Assert.Contains("10", result.Message);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendInteractiveListAsync_ButtonTextDeMasDeVeinteCaracteres_FallaSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var sections = new[]
        {
            ("Sección", (IEnumerable<(string Id, string Title, string? Description)>)new[] { ("op1", "Opción 1", (string?)null) })
        };

        var result = await client.SendInteractiveListAsync("5491100000000", "Elegí una opción", "Este texto de botón tiene más de veinte caracteres", sections);

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendInteractiveListAsync_DescripcionDeMasDeSetentaYDosCaracteres_FallaSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var longDescription = new string('a', 73);
        var sections = new[]
        {
            ("Sección", (IEnumerable<(string Id, string Title, string? Description)>)new[] { ("op1", "Opción 1", (string?)longDescription) })
        };

        var result = await client.SendInteractiveListAsync("5491100000000", "Elegí una opción", "Ver opciones", sections);

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }
}
