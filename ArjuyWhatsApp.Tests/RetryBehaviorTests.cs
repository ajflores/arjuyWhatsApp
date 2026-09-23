using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

/// <summary>
/// Tests del retry con backoff exponencial ante errores transitorios de Meta (HTTP 429 y 5xx).
/// Usa <see cref="ArjuyWhatsAppOptions.BaseRetryDelay"/> en milisegundos bajos para no hacer la
/// suite lenta — no se testean tiempos reales de producción, solo la cantidad de intentos y la
/// lógica de decisión de reintentar o no.
/// </summary>
public class RetryBehaviorTests
{
    private static ArjuyWhatsAppOptions BuildOptions(int maxRetryAttempts = 3)
    {
        return new ArjuyWhatsAppOptions
        {
            AccessToken = "fake-token",
            PhoneNumberId = "1234567890",
            ApiVersion = "v21.0",
            MaxRetryAttempts = maxRetryAttempts,
            BaseRetryDelay = TimeSpan.FromMilliseconds(5)
        };
    }

    private static IServiceScopeFactory BuildEmptyScopeFactory()
    {
        return new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static ArjuyWhatsAppClient BuildClient(IHttpClientFactory factory, ArjuyWhatsAppOptions options)
    {
        return new ArjuyWhatsAppClient(
            factory,
            Options.Create(options),
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

    private static string ErrorBody(string message, int code) => JsonSerializer.Serialize(new
    {
        error = new { message, type = "OAuthException", code }
    });

    [Fact]
    public async Task SendTextAsync_PrimerIntento429_SegundoIntentoExitoso_ReintentaYDevuelveExito()
    {
        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.TooManyRequests, ErrorBody("Rate limit hit", 130429), (TimeSpan?)null),
            (HttpStatusCode.OK, SuccessBody("wamid.RETRY_OK"), (TimeSpan?)null)
        });
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, BuildOptions());

        var result = await client.SendTextAsync("5491100000000", "Hola");

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.RETRY_OK", result.Data);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SendTextAsync_Siempre429_AgotaIntentosYDevuelveElUltimoFail()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, ErrorBody("Rate limit hit", 130429));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, BuildOptions(maxRetryAttempts: 3));

        var result = await client.SendTextAsync("5491100000000", "Hola");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.True(result.Error!.IsRateLimited);
        // 1 intento inicial + 3 reintentos = 4 llamados HTTP en total.
        Assert.Equal(4, handler.CallCount);
    }

    [Fact]
    public async Task SendTextAsync_Error400_NoReintenta()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, ErrorBody("Invalid parameter", 100));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, BuildOptions(maxRetryAttempts: 3));

        var result = await client.SendTextAsync("5491100000000", "Hola");

        Assert.False(result.IsSuccess);
        Assert.False(result.Error!.IsTransient);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendTextAsync_MaxRetryAttemptsCero_NoReintentaNiEnErrorTransitorio()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.ServiceUnavailable, ErrorBody("Service unavailable", 2));
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, BuildOptions(maxRetryAttempts: 0));

        var result = await client.SendTextAsync("5491100000000", "Hola");

        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendTextAsync_Error500_EsTransitorioYReintenta()
    {
        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.InternalServerError, "Internal Server Error", (TimeSpan?)null),
            (HttpStatusCode.OK, SuccessBody("wamid.AFTER_500"), (TimeSpan?)null)
        });
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, BuildOptions());

        var result = await client.SendTextAsync("5491100000000", "Hola");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SendTextAsync_RespetaRetryAfterDelHeaderEnVezDelBackoffCalculado()
    {
        // BaseRetryDelay queda en 5ms (backoff calculado sería ~5-6ms con jitter), pero el
        // Retry-After del header dice 0 segundos explícitamente — si el código lo respeta,
        // el tiempo total no debería depender del BaseRetryDelay configurado.
        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.TooManyRequests, ErrorBody("Rate limit hit", 130429), (TimeSpan?)TimeSpan.Zero),
            (HttpStatusCode.OK, SuccessBody("wamid.RETRY_AFTER_OK"), (TimeSpan?)null)
        });
        var factory = new FakeHttpClientFactory(handler);
        // BaseRetryDelay grande a propósito: si el código IGNORARA el Retry-After y usara el
        // backoff calculado, este test tardaría ~2 segundos. Con Retry-After=0 respetado, es casi
        // instantáneo.
        var options = BuildOptions();
        options.BaseRetryDelay = TimeSpan.FromSeconds(2);
        var client = BuildClient(factory, options);

        var start = DateTime.UtcNow;
        var result = await client.SendTextAsync("5491100000000", "Hola");
        var elapsed = DateTime.UtcNow - start;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.CallCount);
        Assert.True(elapsed < TimeSpan.FromSeconds(1), $"Se esperaba que se respetara Retry-After=0 en vez del backoff de 2s configurado; tardó {elapsed}.");
    }

    [Fact]
    public async Task UploadMediaAsync_PrimerIntento429_ReintentaYDevuelveExito()
    {
        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.TooManyRequests, ErrorBody("Rate limit hit", 130429), (TimeSpan?)null),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new { id = "MEDIA_RETRY_OK" }), (TimeSpan?)null)
        });
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, BuildOptions());

        var result = await client.UploadMediaAsync(new byte[] { 1, 2, 3 }, "archivo.pdf", "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("MEDIA_RETRY_OK", result.Data);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task DownloadMediaAsync_PrimerIntento429EnPrimerPaso_ReintentaYDevuelveExito()
    {
        var mediaUrlBody = JsonSerializer.Serialize(new { url = "https://graph.facebook.com/fake-download" });
        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.TooManyRequests, ErrorBody("Rate limit hit", 130429), (TimeSpan?)null),
            (HttpStatusCode.OK, mediaUrlBody, (TimeSpan?)null),
            (HttpStatusCode.OK, "contenido-binario-simulado", (TimeSpan?)null)
        });
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, BuildOptions());

        var result = await client.DownloadMediaAsync("MEDIA_ID_1");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task SendInteractiveButtonsAsync_ValidacionLocalFallida_NuncaReintenta()
    {
        // Un Fail(string) de validación local (sin MetaApiError) no tiene que disparar retry —
        // no hay ni siquiera un llamado HTTP de por medio.
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, BuildOptions(maxRetryAttempts: 3));

        var buttons = new[] { ("op1", "Uno"), ("op2", "Dos"), ("op3", "Tres"), ("op4", "Cuatro") };
        var result = await client.SendInteractiveButtonsAsync("5491100000000", "Elegí", buttons);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(0, handler.CallCount);
    }
}
