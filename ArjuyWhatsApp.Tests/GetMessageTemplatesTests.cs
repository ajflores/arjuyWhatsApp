using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArjuyWhatsApp.Tests;

/// <summary>
/// Tests de <see cref="ArjuyWhatsAppClient.GetMessageTemplatesAsync"/>: parseo de plantillas y
/// componentes, paginación automática siguiendo <c>paging.next</c>, y manejo de errores.
/// </summary>
public class GetMessageTemplatesTests
{
    private static ArjuyWhatsAppOptions BuildOptions()
    {
        return new ArjuyWhatsAppOptions
        {
            AccessToken = "fake-token",
            PhoneNumberId = "1234567890",
            BusinessAccountId = "999888777",
            ApiVersion = "v21.0",
            MaxRetryAttempts = 0
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

    private const string PageOneBody = """
        {
          "data": [
            {
              "id": "111",
              "name": "confirmacion_reserva",
              "language": "es_AR",
              "status": "APPROVED",
              "category": "UTILITY",
              "components": [
                { "type": "HEADER", "format": "TEXT", "text": "Reserva confirmada" },
                { "type": "BODY", "text": "Hola {{1}}, tu reserva para el {{2}} está confirmada." },
                { "type": "FOOTER", "text": "Gracias por elegirnos" },
                { "type": "BUTTONS", "buttons": [ { "type": "QUICK_REPLY", "text": "Ver detalle" } ] }
              ]
            },
            {
              "id": "222",
              "name": "plantilla_rechazada",
              "language": "es",
              "status": "REJECTED",
              "category": "MARKETING",
              "rejected_reason": "INVALID_FORMAT",
              "components": [
                { "type": "BODY", "text": "Promo sin parámetros." }
              ]
            }
          ],
          "paging": { "cursors": { "before": "a", "after": "b" }, "next": "https://graph.facebook.com/v21.0/999888777/message_templates?after=b" }
        }
        """;

    private const string PageTwoBody = """
        {
          "data": [
            {
              "id": "333",
              "name": "recordatorio_pago",
              "language": "es_AR",
              "status": "PAUSED",
              "category": "UTILITY",
              "components": [
                { "type": "HEADER", "format": "DOCUMENT" },
                { "type": "BODY", "text": "Recordamos tu pago de {{1}} vence el {{2}}. Referencia {{3}}." },
                {
                  "type": "BUTTONS",
                  "buttons": [
                    { "type": "URL", "text": "Pagar ahora", "url": "https://pagos.ejemplo.com/{{1}}" },
                    { "type": "PHONE_NUMBER", "text": "Llamar", "phone_number": "+5491100000000" }
                  ]
                }
              ]
            }
          ],
          "paging": { "cursors": { "before": "b", "after": "c" } }
        }
        """;

    [Fact]
    public async Task GetMessageTemplatesAsync_UnaSolaPagina_ParseaPlantillasYComponentesCorrectamente()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, PageTwoBody);
        var client = BuildClient(new FakeHttpClientFactory(handler), BuildOptions());

        var result = await client.GetMessageTemplatesAsync();

        Assert.True(result.IsSuccess);
        var template = Assert.Single(result.Data!);
        Assert.Equal("333", template.Id);
        Assert.Equal("recordatorio_pago", template.Name);
        Assert.Equal("es_AR", template.Language);
        Assert.Equal(WhatsAppMessageTemplateStatus.Paused, template.Status);
        Assert.Equal("UTILITY", template.Category);
        Assert.Null(template.RejectedReason);

        Assert.Equal(3, template.Components.Count);

        var header = template.Components[0];
        Assert.Equal(WhatsAppTemplateComponentType.Header, header.Type);
        Assert.Equal("DOCUMENT", header.Format);
        Assert.Null(header.Text);

        var body = template.Components[1];
        Assert.Equal(WhatsAppTemplateComponentType.Body, body.Type);
        Assert.Equal(3, body.ParameterCount);
        Assert.Equal(3, template.BodyParameterCount);

        var buttons = template.Components[2];
        Assert.Equal(WhatsAppTemplateComponentType.Buttons, buttons.Type);
        Assert.Equal(2, buttons.Buttons.Count);
        Assert.Equal("URL", buttons.Buttons[0].Type);
        Assert.Equal("https://pagos.ejemplo.com/{{1}}", buttons.Buttons[0].Url);
        Assert.Equal("PHONE_NUMBER", buttons.Buttons[1].Type);
        Assert.Equal("+5491100000000", buttons.Buttons[1].PhoneNumber);
    }

    [Fact]
    public async Task GetMessageTemplatesAsync_StatusRejectedConMotivo_MapeaRejectedReason()
    {
        // PageOneBody trae "paging.next" — hace falta una segunda respuesta sin "next" para que la
        // paginación corte (si no, el fake repite PageOneBody indefinidamente y duplica resultados).
        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.OK, PageOneBody, (TimeSpan?)null),
            (HttpStatusCode.OK, PageTwoBody, (TimeSpan?)null)
        });
        var client = BuildClient(new FakeHttpClientFactory(handler), BuildOptions());

        var result = await client.GetMessageTemplatesAsync();

        var rejected = result.Data!.Single(t => t.Name == "plantilla_rechazada");
        Assert.Equal(WhatsAppMessageTemplateStatus.Rejected, rejected.Status);
        Assert.Equal("INVALID_FORMAT", rejected.RejectedReason);
        Assert.Equal(0, rejected.BodyParameterCount);
    }

    [Fact]
    public async Task GetMessageTemplatesAsync_DosPaginas_SigueLaPaginacionYDevuelveTodas()
    {
        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.OK, PageOneBody, (TimeSpan?)null),
            (HttpStatusCode.OK, PageTwoBody, (TimeSpan?)null)
        });
        var client = BuildClient(new FakeHttpClientFactory(handler), BuildOptions());

        var result = await client.GetMessageTemplatesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Count);
        Assert.Equal(new[] { "111", "222", "333" }, result.Data!.Select(t => t.Id));
        Assert.Equal(2, handler.CallCount);

        // La segunda request tiene que apuntar a la URL de paging.next de la primera página, no reconstruirla a mano.
        Assert.Equal("https://graph.facebook.com/v21.0/999888777/message_templates?after=b", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetMessageTemplatesAsync_FaltaBusinessAccountId_DevuelveFailSinLlamarAMeta()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, PageTwoBody);
        var options = BuildOptions();
        options.BusinessAccountId = string.Empty;
        var client = BuildClient(new FakeHttpClientFactory(handler), options);

        var result = await client.GetMessageTemplatesAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetMessageTemplatesAsync_ErrorDeMetaEnSegundaPagina_DevuelveFailSinDatosParciales()
    {
        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.OK, PageOneBody, (TimeSpan?)null),
            (HttpStatusCode.Unauthorized, """{"error":{"message":"Token inválido","type":"OAuthException","code":190}}""", (TimeSpan?)null)
        });
        var client = BuildClient(new FakeHttpClientFactory(handler), BuildOptions());

        var result = await client.GetMessageTemplatesAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(190, result.Error!.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetMessageTemplatesAsync_ErrorTransitorio429_ReintentaYDevuelveExito()
    {
        var handler = new FakeHttpMessageHandler(new[]
        {
            (HttpStatusCode.TooManyRequests, """{"error":{"message":"Rate limit hit","type":"OAuthException","code":130429}}""", (TimeSpan?)null),
            (HttpStatusCode.OK, PageTwoBody, (TimeSpan?)null)
        });
        var options = BuildOptions();
        options.MaxRetryAttempts = 3;
        options.BaseRetryDelay = TimeSpan.FromMilliseconds(5);
        var client = BuildClient(new FakeHttpClientFactory(handler), options);

        var result = await client.GetMessageTemplatesAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(2, handler.CallCount);
    }
}
