using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

public class ReactionLocationContactsTests
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

    // ----------------------- SendReactionAsync -----------------------

    [Fact]
    public async Task SendReactionAsync_ArmaPayloadCorrecto()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendReactionAsync("5491100000000", "wamid.IN1", "👍");

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.OUT1", result.Data);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("reaction", root.GetProperty("type").GetString());
        Assert.Equal("wamid.IN1", root.GetProperty("reaction").GetProperty("message_id").GetString());
        Assert.Equal("👍", root.GetProperty("reaction").GetProperty("emoji").GetString());
        Assert.False(root.TryGetProperty("context", out _));
    }

    [Fact]
    public async Task SendReactionAsync_EmojiVacio_MandaEmojiVacioParaRemoverReaccion()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendReactionAsync("5491100000000", "wamid.IN1", string.Empty);

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(string.Empty, bodyDoc.RootElement.GetProperty("reaction").GetProperty("emoji").GetString());
    }

    // ----------------------- SendLocationAsync -----------------------

    [Fact]
    public async Task SendLocationAsync_ArmaPayloadCorrecto_ConReply()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendLocationAsync(
            "5491100000000", 37.44216251868683, -122.16153582049394,
            name: "Philz Coffee", address: "101 Forest Ave, Palo Alto, CA", replyToMessageId: "wamid.IN2");

        Assert.True(result.IsSuccess);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("location", root.GetProperty("type").GetString());
        var location = root.GetProperty("location");
        Assert.Equal(37.44216251868683, location.GetProperty("latitude").GetDouble(), 10);
        Assert.Equal(-122.16153582049394, location.GetProperty("longitude").GetDouble(), 10);
        Assert.Equal("Philz Coffee", location.GetProperty("name").GetString());
        Assert.Equal("101 Forest Ave, Palo Alto, CA", location.GetProperty("address").GetString());
        Assert.Equal("wamid.IN2", root.GetProperty("context").GetProperty("message_id").GetString());
    }

    [Fact]
    public async Task SendLocationAsync_SinNombreNiDireccion_MandaCamposVacios()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendLocationAsync("5491100000000", 1.0, 2.0);

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var location = bodyDoc.RootElement.GetProperty("location");
        Assert.Equal(string.Empty, location.GetProperty("name").GetString());
        Assert.Equal(string.Empty, location.GetProperty("address").GetString());
    }

    // ----------------------- SendContactsAsync -----------------------

    [Fact]
    public async Task SendContactsAsync_ArmaPayloadCorrecto_ConTodasLasSecciones()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var contact = new WhatsAppContact
        {
            Name = new WhatsAppContactName { FormattedName = "Juan Pérez", FirstName = "Juan", LastName = "Pérez" },
            Phones = [new WhatsAppContactPhone { Phone = "+5491100000000", WaId = "5491100000000", Type = "WORK" }],
            Emails = [new WhatsAppContactEmail { Email = "juan@example.com", Type = "WORK" }],
            Addresses = [new WhatsAppContactAddress { City = "San Salvador de Jujuy", Country = "Argentina" }],
            Org = new WhatsAppContactOrg { Company = "ArjuyDev" },
            Birthday = "2001-01-01"
        };

        var result = await client.SendContactsAsync("5491100000000", [contact]);

        Assert.True(result.IsSuccess);

        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = bodyDoc.RootElement;
        Assert.Equal("contacts", root.GetProperty("type").GetString());

        var contactsArray = root.GetProperty("contacts");
        Assert.Equal(1, contactsArray.GetArrayLength());
        var contactElement = contactsArray[0];
        Assert.Equal("Juan Pérez", contactElement.GetProperty("name").GetProperty("formatted_name").GetString());
        Assert.Equal("+5491100000000", contactElement.GetProperty("phones")[0].GetProperty("phone").GetString());
        Assert.Equal("juan@example.com", contactElement.GetProperty("emails")[0].GetProperty("email").GetString());
        Assert.Equal("San Salvador de Jujuy", contactElement.GetProperty("addresses")[0].GetProperty("city").GetString());
        Assert.Equal("ArjuyDev", contactElement.GetProperty("org").GetProperty("company").GetString());
        Assert.Equal("2001-01-01", contactElement.GetProperty("birthday").GetString());
    }

    [Fact]
    public async Task SendContactsAsync_SoloFormattedName_OmiteSeccionesVacias()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var contact = new WhatsAppContact { Name = new WhatsAppContactName { FormattedName = "Solo Nombre" } };

        var result = await client.SendContactsAsync("5491100000000", [contact]);

        Assert.True(result.IsSuccess);
        using var bodyDoc = JsonDocument.Parse(handler.LastRequestBody!);
        var contactElement = bodyDoc.RootElement.GetProperty("contacts")[0];
        Assert.False(contactElement.TryGetProperty("phones", out _));
        Assert.False(contactElement.TryGetProperty("emails", out _));
        Assert.False(contactElement.TryGetProperty("addresses", out _));
        Assert.False(contactElement.TryGetProperty("org", out _));
        Assert.False(contactElement.TryGetProperty("birthday", out _));
    }

    [Fact]
    public async Task SendContactsAsync_SinContactos_FallaLocalmenteSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var result = await client.SendContactsAsync("5491100000000", []);

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendContactsAsync_FormattedNameVacio_FallaLocalmenteSinLlamarHttp()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        var factory = new FakeHttpClientFactory(handler);
        var client = BuildClient(factory, Options.Create(BuildOptions()));

        var contact = new WhatsAppContact { Name = new WhatsAppContactName { FormattedName = "" } };
        var result = await client.SendContactsAsync("5491100000000", [contact]);

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }
}
