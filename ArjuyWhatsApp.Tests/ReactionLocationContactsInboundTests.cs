using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

/// <summary>Tests del parseo de mensajes entrantes de tipo "location", "contacts" y "reaction" vía <see cref="ArjuyWhatsAppClient.ProcessWebhookAsync"/>.</summary>
public class ReactionLocationContactsInboundTests
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
    public async Task ProcessWebhookAsync_MensajeLocation_ParseaCoordenadasNombreYDireccion()
    {
        var messageJson = """
        {
          "from": "5491100000000",
          "id": "wamid.LOC1",
          "timestamp": "1700000000",
          "type": "location",
          "location": { "latitude": 37.44216251868683, "longitude": -122.16153582049394, "name": "Philz Coffee", "address": "101 Forest Ave" }
        }
        """;
        var rawBody = WrapMessage(messageJson);
        var client = BuildClient();

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        var result = await client.ProcessWebhookAsync(rawBody, ComputeSignatureHeader(rawBody, AppSecret));

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal(WhatsAppMessageType.Location, received!.Type);
        Assert.NotNull(received.Location);
        Assert.Equal(37.44216251868683, received.Location!.Latitude, 10);
        Assert.Equal(-122.16153582049394, received.Location.Longitude, 10);
        Assert.Equal("Philz Coffee", received.Location.Name);
        Assert.Equal("101 Forest Ave", received.Location.Address);
    }

    [Fact]
    public async Task ProcessWebhookAsync_MensajeReaction_ParseaEmojiYMessageIdReaccionado()
    {
        var messageJson = """
        {
          "from": "5491100000000",
          "id": "wamid.REACT1",
          "timestamp": "1700000000",
          "type": "reaction",
          "reaction": { "message_id": "wamid.ORIGINAL1", "emoji": "🔥" }
        }
        """;
        var rawBody = WrapMessage(messageJson);
        var client = BuildClient();

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        var result = await client.ProcessWebhookAsync(rawBody, ComputeSignatureHeader(rawBody, AppSecret));

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal(WhatsAppMessageType.Reaction, received!.Type);
        Assert.Equal("🔥", received.ReactionEmoji);
        Assert.Equal("wamid.ORIGINAL1", received.ReactionToMessageId);
    }

    [Fact]
    public async Task ProcessWebhookAsync_MensajeContacts_ParseaContactoConTodasLasSecciones()
    {
        var messageJson = """
        {
          "from": "5491100000000",
          "id": "wamid.CONTACT1",
          "timestamp": "1700000000",
          "type": "contacts",
          "contacts": [
            {
              "name": { "formatted_name": "Ana Gómez", "first_name": "Ana", "last_name": "Gómez" },
              "phones": [ { "phone": "+5491100000001", "wa_id": "5491100000001", "type": "WORK" } ],
              "emails": [ { "email": "ana@example.com", "type": "WORK" } ],
              "org": { "company": "ArjuyDev" }
            }
          ]
        }
        """;
        var rawBody = WrapMessage(messageJson);
        var client = BuildClient();

        WhatsAppMessageReceived? received = null;
        client.MessageReceived += (_, args) => received = args.Message;

        var result = await client.ProcessWebhookAsync(rawBody, ComputeSignatureHeader(rawBody, AppSecret));

        Assert.True(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal(WhatsAppMessageType.Contacts, received!.Type);
        Assert.Single(received.Contacts);
        var contact = received.Contacts[0];
        Assert.Equal("Ana Gómez", contact.Name.FormattedName);
        Assert.Equal("+5491100000001", contact.Phones[0].Phone);
        Assert.Equal("ana@example.com", contact.Emails[0].Email);
        Assert.Equal("ArjuyDev", contact.Org!.Company);
    }
}
