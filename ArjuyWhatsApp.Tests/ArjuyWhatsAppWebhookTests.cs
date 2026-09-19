using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

public class ArjuyWhatsAppWebhookTests
{
    private const string AppSecret = "fake-app-secret";
    private const string VerifyToken = "fake-verify-token";

    private static ArjuyWhatsAppOptions BuildOptions()
    {
        return new ArjuyWhatsAppOptions
        {
            AccessToken = "fake-token",
            PhoneNumberId = "1234567890",
            AppSecret = AppSecret,
            VerifyToken = VerifyToken,
            ApiVersion = "v21.0"
        };
    }

    private static IServiceScopeFactory BuildEmptyScopeFactory()
    {
        return new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static ArjuyWhatsAppClient BuildClient(ArjuyWhatsAppOptions options, IServiceScopeFactory? scopeFactory = null)
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var factory = new FakeHttpClientFactory(handler);

        return new ArjuyWhatsAppClient(
            factory,
            Options.Create(options),
            scopeFactory ?? BuildEmptyScopeFactory(),
            NullLogger<ArjuyWhatsAppClient>.Instance);
    }

    /// <summary>Payload real (forma verificada contra el WebhooksController de producción de ArjuyTurismo) con un único mensaje de texto entrante.</summary>
    private static string BuildTextMessagePayload(string messageId = "wamid.ABC123", string from = "5491100000000", string text = "Hola")
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
                    "contacts": [
                      { "profile": { "name": "Juan" }, "wa_id": "{{from}}" }
                    ],
                    "messages": [
                      {
                        "from": "{{from}}",
                        "id": "{{messageId}}",
                        "timestamp": "1700000000",
                        "type": "text",
                        "text": { "body": "{{text}}" }
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
        """;
    }

    /// <summary>Payload real (forma verificada contra la documentación oficial de Meta — ver ArjuyWhatsApp/WebhookPayload.cs) con un único mensaje entrante de respuesta a un botón interactivo.</summary>
    private static string BuildInteractiveButtonReplyPayload(string buttonId, string buttonTitle, string messageId = "wamid.BTN1", string from = "5491100000000")
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
                    "contacts": [
                      { "profile": { "name": "Juan" }, "wa_id": "{{from}}" }
                    ],
                    "messages": [
                      {
                        "from": "{{from}}",
                        "id": "{{messageId}}",
                        "timestamp": "1700000000",
                        "type": "interactive",
                        "interactive": {
                          "type": "button_reply",
                          "button_reply": { "id": "{{buttonId}}", "title": "{{buttonTitle}}" }
                        }
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
        """;
    }

    /// <summary>Payload real (forma verificada contra la documentación oficial de Meta — ver ArjuyWhatsApp/WebhookPayload.cs) con un único mensaje entrante de respuesta a una fila de lista interactiva.</summary>
    private static string BuildInteractiveListReplyPayload(string rowId, string rowTitle, string messageId = "wamid.LST1", string from = "5491100000000")
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
                    "contacts": [
                      { "profile": { "name": "Juan" }, "wa_id": "{{from}}" }
                    ],
                    "messages": [
                      {
                        "from": "{{from}}",
                        "id": "{{messageId}}",
                        "timestamp": "1700000000",
                        "type": "interactive",
                        "interactive": {
                          "type": "list_reply",
                          "list_reply": { "id": "{{rowId}}", "title": "{{rowTitle}}", "description": "Descripción de la fila" }
                        }
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
        """;
    }

    /// <summary>Payload real (forma verificada contra la documentación oficial de Meta — ver ArjuyWhatsApp/WebhookPayload.cs) con una única actualización de estado "delivered".</summary>
    private static string BuildStatusPayload(string status, string messageId = "wamid.STATUS1", string recipientId = "5491100000000", string? errorsJson = null)
    {
        var errorsSection = errorsJson is null ? string.Empty : $$""", "errors": {{errorsJson}}""";

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
                    "statuses": [
                      {
                        "id": "{{messageId}}",
                        "status": "{{status}}",
                        "timestamp": "1700000000",
                        "recipient_id": "{{recipientId}}"{{errorsSection}}
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
        """;
    }

    private static string ComputeSignatureHeader(string rawBody, string appSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void VerifyWebhookChallenge_ModeYTokenValidos_DevuelveChallenge()
    {
        var client = BuildClient(BuildOptions());

        var result = client.VerifyWebhookChallenge("subscribe", VerifyToken, "el-challenge-de-meta");

        Assert.Equal("el-challenge-de-meta", result);
    }

    [Fact]
    public void VerifyWebhookChallenge_TokenInvalido_DevuelveNull()
    {
        var client = BuildClient(BuildOptions());

        var result = client.VerifyWebhookChallenge("subscribe", "token-incorrecto", "el-challenge-de-meta");

        Assert.Null(result);
    }

    [Fact]
    public void VerifyWebhookChallenge_ModeDistintoDeSubscribe_DevuelveNull()
    {
        var client = BuildClient(BuildOptions());

        var result = client.VerifyWebhookChallenge("unsubscribe", VerifyToken, "el-challenge-de-meta");

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessWebhookAsync_FirmaValida_DisparaEventoMessageReceived()
    {
        var client = BuildClient(BuildOptions());
        var rawBody = BuildTextMessagePayload(messageId: "wamid.EVENT1", from: "5491100000001", text: "Hola desde el evento");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal("wamid.EVENT1", received!.MessageId);
        Assert.Equal("5491100000001", received.From);
        Assert.Equal(WhatsAppMessageType.Text, received.Type);
        Assert.Equal("Hola desde el evento", received.Text);
    }

    [Fact]
    public async Task ProcessWebhookAsync_FirmaInvalida_DevuelveFailYNoDisparaEvento()
    {
        var client = BuildClient(BuildOptions());
        var rawBody = BuildTextMessagePayload();
        var signature = "sha256=" + new string('0', 64); // firma claramente incorrecta

        var eventFired = false;
        client.MessageReceived += (_, _) => eventFired = true;

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.False(result.IsSuccess);
        Assert.False(eventFired);
    }

    [Fact]
    public async Task ProcessWebhookAsync_SinSignatureHeader_DevuelveFail()
    {
        var client = BuildClient(BuildOptions());
        var rawBody = BuildTextMessagePayload();

        var result = await client.ProcessWebhookAsync(rawBody, signatureHeader: null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ConHandlerRegistradoPorDI_InvocaAlHandler()
    {
        var received = new List<WhatsAppMessageReceived>();

        var services = new ServiceCollection();
        services.AddSingleton(received);
        services.AddScoped<IWhatsAppMessageHandler, FakeWhatsAppMessageHandler>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var client = BuildClient(BuildOptions(), scopeFactory);
        var rawBody = BuildTextMessagePayload(messageId: "wamid.DI1", from: "5491100000002", text: "Hola por DI");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.Single(received);
        Assert.Equal("wamid.DI1", received[0].MessageId);
        Assert.Equal("5491100000002", received[0].From);
    }

    [Fact]
    public async Task ProcessWebhookAsync_HandlerLanzaExcepcion_NoRompeElProcesamientoNiAOtrosHandlers()
    {
        var received = new List<WhatsAppMessageReceived>();

        var services = new ServiceCollection();
        services.AddSingleton(received);
        services.AddScoped<IWhatsAppMessageHandler, ThrowingWhatsAppMessageHandler>();
        services.AddScoped<IWhatsAppMessageHandler, FakeWhatsAppMessageHandler>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var client = BuildClient(BuildOptions(), scopeFactory);
        var rawBody = BuildTextMessagePayload(messageId: "wamid.DI2");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.Single(received);
    }

    [Fact]
    public async Task ProcessWebhookAsync_StatusDelivered_ParseaYDisparaEventoMessageStatusUpdated()
    {
        var client = BuildClient(BuildOptions());
        var rawBody = BuildStatusPayload("delivered", messageId: "wamid.DELIVERED1", recipientId: "5491100000003");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        WhatsAppMessageStatusUpdate? received = null;
        client.MessageStatusUpdated += (_, args) => received = args.StatusUpdate;

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal("wamid.DELIVERED1", received!.MessageId);
        Assert.Equal("5491100000003", received.RecipientPhoneNumber);
        Assert.Equal(WhatsAppMessageStatus.Delivered, received.Status);
        Assert.Null(received.ErrorCode);
        Assert.Null(received.ErrorMessage);
    }

    [Fact]
    public async Task ProcessWebhookAsync_StatusFailed_ParseaErrorCodeYErrorMessage()
    {
        var client = BuildClient(BuildOptions());
        var errorsJson = """[ { "code": 131026, "title": "Message undeliverable", "message": "El destinatario no tiene WhatsApp habilitado" } ]""";
        var rawBody = BuildStatusPayload("failed", messageId: "wamid.FAILED1", errorsJson: errorsJson);
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        WhatsAppMessageStatusUpdate? received = null;
        client.MessageStatusUpdated += (_, args) => received = args.StatusUpdate;

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal(WhatsAppMessageStatus.Failed, received!.Status);
        Assert.Equal(131026, received.ErrorCode);
        Assert.Equal("El destinatario no tiene WhatsApp habilitado", received.ErrorMessage);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ConHandlerDeStatusRegistradoPorDI_InvocaAlHandler()
    {
        var received = new List<WhatsAppMessageStatusUpdate>();

        var services = new ServiceCollection();
        services.AddSingleton(received);
        services.AddScoped<IWhatsAppStatusHandler, FakeWhatsAppStatusHandler>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var client = BuildClient(BuildOptions(), scopeFactory);
        var rawBody = BuildStatusPayload("read", messageId: "wamid.STATUSDI1");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.Single(received);
        Assert.Equal("wamid.STATUSDI1", received[0].MessageId);
        Assert.Equal(WhatsAppMessageStatus.Read, received[0].Status);
    }

    [Fact]
    public async Task ProcessWebhookAsync_StatusHandlerLanzaExcepcion_NoRompeElProcesamientoNiAOtrosHandlers()
    {
        var received = new List<WhatsAppMessageStatusUpdate>();

        var services = new ServiceCollection();
        services.AddSingleton(received);
        services.AddScoped<IWhatsAppStatusHandler, ThrowingWhatsAppStatusHandler>();
        services.AddScoped<IWhatsAppStatusHandler, FakeWhatsAppStatusHandler>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var client = BuildClient(BuildOptions(), scopeFactory);
        var rawBody = BuildStatusPayload("sent", messageId: "wamid.STATUSDI2");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.Single(received);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PayloadConMessages_NoDisparaEventoDeStatus()
    {
        var client = BuildClient(BuildOptions());
        var rawBody = BuildTextMessagePayload(messageId: "wamid.ONLYMSG1");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        var statusEventFired = false;
        client.MessageStatusUpdated += (_, _) => statusEventFired = true;

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.False(statusEventFired);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PayloadConStatuses_NoDisparaEventoDeMensajeEntrante()
    {
        var client = BuildClient(BuildOptions());
        var rawBody = BuildStatusPayload("sent", messageId: "wamid.ONLYSTATUS1");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        var messageEventFired = false;
        client.MessageReceived += (_, _) => messageEventFired = true;

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.False(messageEventFired);
    }

    [Fact]
    public async Task ProcessWebhookAsync_RespuestaDeBoton_PopulaInteractiveReplyIdYTitle()
    {
        var client = BuildClient(BuildOptions());
        var rawBody = BuildInteractiveButtonReplyPayload(buttonId: "confirmar", buttonTitle: "Confirmar", messageId: "wamid.BTNEVENT1");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal(WhatsAppMessageType.Interactive, received!.Type);
        Assert.Equal("confirmar", received.InteractiveReplyId);
        Assert.Equal("Confirmar", received.InteractiveReplyTitle);
    }

    [Fact]
    public async Task ProcessWebhookAsync_RespuestaDeLista_PopulaInteractiveReplyIdYTitle()
    {
        var client = BuildClient(BuildOptions());
        var rawBody = BuildInteractiveListReplyPayload(rowId: "exc1", rowTitle: "Full day glaciar", messageId: "wamid.LSTEVENT1");
        var signature = ComputeSignatureHeader(rawBody, AppSecret);

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        var result = await client.ProcessWebhookAsync(rawBody, signature);

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal(WhatsAppMessageType.Interactive, received!.Type);
        Assert.Equal("exc1", received.InteractiveReplyId);
        Assert.Equal("Full day glaciar", received.InteractiveReplyTitle);
    }
}
