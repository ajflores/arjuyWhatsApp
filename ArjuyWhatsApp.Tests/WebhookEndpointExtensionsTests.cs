using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArjuyWhatsApp.Tests;

/// <summary>
/// Tests de integración de <see cref="WebhookEndpointExtensions.MapArjuyWhatsAppWebhook"/> usando
/// <see cref="TestServer"/> (alternativa liviana a <c>WebApplicationFactory</c>, sin necesitar un
/// proyecto de API real ni levantar Kestrel): confirma que el GET y el POST responden lo mismo que
/// respondía el <c>WhatsAppWebhookController</c> a mano que reemplaza.
/// </summary>
public class WebhookEndpointExtensionsTests
{
    private const string AppSecret = "fake-app-secret";
    private const string VerifyToken = "fake-verify-token";

    private static TestServer BuildServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddArjuyWhatsApp(options =>
                {
                    options.AccessToken = "fake-token";
                    options.PhoneNumberId = "1234567890";
                    options.AppSecret = AppSecret;
                    options.VerifyToken = VerifyToken;
                    options.ApiVersion = "v21.0";
                });
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapArjuyWhatsAppWebhook());
            });

        return new TestServer(builder);
    }

    private static string ComputeSignatureHeader(string rawBody, string appSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public async Task Get_TokenValido_Devuelve200ConChallengeEnTextoPlano()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync(
            $"/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token={VerifyToken}&hub.challenge=el-challenge-de-meta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("el-challenge-de-meta", await response.Content.ReadAsStringAsync());
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_TokenInvalido_Devuelve403()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync(
            "/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=token-incorrecto&hub.challenge=el-challenge-de-meta");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_FirmaValida_Devuelve200()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();

        const string rawBody = """
        {
          "object": "whatsapp_business_account",
          "entry": []
        }
        """;
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);

        var response = await client.PostAsync("/api/webhooks/whatsapp", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_FirmaInvalida_Devuelve403()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();

        const string rawBody = """
        {
          "object": "whatsapp_business_account",
          "entry": []
        }
        """;

        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", "sha256=" + new string('0', 64));

        var response = await client.PostAsync("/api/webhooks/whatsapp", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
