using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

public class MarkAsReadTests
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

    [Fact]
    public async Task MarkAsReadAsync_RespuestaExitosa_ArmaPayloadYUrlCorrectos()
    {
        var responseBody = JsonSerializer.Serialize(new { success = true });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var result = await client.MarkAsReadAsync("wamid.ABC123");

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://graph.facebook.com/v21.0/1234567890/messages", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("fake-token", handler.LastRequest.Headers.Authorization.Parameter);

        Assert.NotNull(handler.LastRequestBody);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("read", root.GetProperty("status").GetString());
        Assert.Equal("wamid.ABC123", root.GetProperty("message_id").GetString());
        Assert.False(root.TryGetProperty("typing_indicator", out _));
    }

    [Fact]
    public async Task MarkAsReadWithTypingIndicatorAsync_RespuestaExitosa_IncluyeTypingIndicatorEnElPayload()
    {
        var responseBody = JsonSerializer.Serialize(new { success = true });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var result = await client.MarkAsReadWithTypingIndicatorAsync("wamid.ABC123");

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        Assert.NotNull(handler.LastRequestBody);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("read", root.GetProperty("status").GetString());
        Assert.Equal("wamid.ABC123", root.GetProperty("message_id").GetString());
        Assert.Equal("text", root.GetProperty("typing_indicator").GetProperty("type").GetString());
    }

    [Fact]
    public async Task MarkAsReadAsync_MessageIdVencidoOInvalido_DevuelveMetaApiErrorEstructurado()
    {
        var errorBody = JsonSerializer.Serialize(new
        {
            error = new
            {
                message = "Message id is invalid or has expired",
                type = "OAuthException",
                code = 131009,
                fbtrace_id = "ABC123"
            }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, errorBody);
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(BuildOptions());
        var client = BuildClient(factory, options);

        var result = await client.MarkAsReadAsync("wamid.VENCIDO");

        Assert.False(result.IsSuccess);
        Assert.False(result.Data);
        Assert.NotNull(result.Error);
        Assert.Equal(400, result.Error!.HttpStatusCode);
        Assert.Equal(131009, result.Error.Code);
        Assert.Equal("Message id is invalid or has expired", result.Error.Message);
        Assert.False(result.Error.IsTransient);
    }

    [Fact]
    public async Task MarkAsReadAsync_SinAccessToken_DevuelveFailSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(new ArjuyWhatsAppOptions { PhoneNumberId = "123" });
        var client = BuildClient(factory, options);

        var result = await client.MarkAsReadAsync("wamid.ABC123");

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task MarkAsReadWithTypingIndicatorAsync_SinPhoneNumberId_DevuelveFailSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(new ArjuyWhatsAppOptions { AccessToken = "fake-token" });
        var client = BuildClient(factory, options);

        var result = await client.MarkAsReadWithTypingIndicatorAsync("wamid.ABC123");

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task MarkAsReadAsync_ErrorTransitorio_ReintentaYEventualmenteTieneExito()
    {
        var errorBody = JsonSerializer.Serialize(new { error = new { message = "Rate limited", code = 80007 } });
        var successBody = JsonSerializer.Serialize(new { success = true });

        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.TooManyRequests, errorBody, (TimeSpan?)null),
            (HttpStatusCode.OK, successBody, (TimeSpan?)null)
        });
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(new ArjuyWhatsAppOptions
        {
            AccessToken = "fake-token",
            PhoneNumberId = "1234567890",
            ApiVersion = "v21.0",
            MaxRetryAttempts = 2,
            BaseRetryDelay = TimeSpan.FromMilliseconds(1)
        });
        var client = BuildClient(factory, options);

        var result = await client.MarkAsReadAsync("wamid.ABC123");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.CallCount);
    }
}
