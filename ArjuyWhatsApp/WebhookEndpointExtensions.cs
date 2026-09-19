using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ArjuyWhatsApp;

/// <summary>
/// Extensiones de Minimal API para registrar el webhook de WhatsApp Cloud API sin que el
/// consumidor tenga que escribir un Controller a mano.
/// </summary>
public static class WebhookEndpointExtensions
{
    /// <summary>
    /// Registra los endpoints <c>GET</c> (handshake de verificación) y <c>POST</c> (recepción de
    /// mensajes/eventos) del webhook de WhatsApp Cloud API en un solo llamado, usando Minimal
    /// APIs. Internamente delega en <see cref="IArjuyWhatsAppClient.VerifyWebhookChallenge"/> y
    /// <see cref="IArjuyWhatsAppClient.ProcessWebhookAsync"/> — ni más ni menos que lo que haría un
    /// Controller escrito a mano (ver MANUAL.md sección 3.3/3.4).
    /// </summary>
    /// <remarks>
    /// Si necesitás lógica adicional en el endpoint (autenticación extra, logging particular,
    /// devolver otro status code, etc.), NO uses este método: armá tu propio Controller/endpoint
    /// llamando a <see cref="IArjuyWhatsAppClient.VerifyWebhookChallenge"/> y
    /// <see cref="IArjuyWhatsAppClient.ProcessWebhookAsync"/> directamente, como en los ejemplos de
    /// MANUAL.md sección 3.3/3.4. Ambos métodos siguen siendo públicos precisamente para eso.
    /// </remarks>
    /// <param name="endpoints">Route builder de la aplicación (típicamente <c>app</c> en <c>Program.cs</c>, tras <c>WebApplication.Build()</c>).</param>
    /// <param name="pattern">Ruta donde se registran ambos endpoints. Por defecto <c>/api/webhooks/whatsapp</c>.</param>
    /// <returns>El mismo <paramref name="endpoints"/>, para encadenar llamadas.</returns>
    public static IEndpointRouteBuilder MapArjuyWhatsAppWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/api/webhooks/whatsapp")
    {
        // GET — handshake de verificación (ver MANUAL.md sección 3.3). Los nombres de query param
        // que manda Meta ("hub.mode", "hub.verify_token", "hub.challenge") tienen puntos y no se
        // pueden bindear como parámetros de método de Minimal API por nombre C# — se toma
        // HttpContext y se leen manualmente desde context.Request.Query.
        endpoints.MapGet(pattern, (HttpContext context, IArjuyWhatsAppClient whatsAppClient) =>
        {
            var mode = context.Request.Query["hub.mode"].ToString();
            var verifyToken = context.Request.Query["hub.verify_token"].ToString();
            var challenge = context.Request.Query["hub.challenge"].ToString();

            var result = whatsAppClient.VerifyWebhookChallenge(mode, verifyToken, challenge);

            // text/plain, no JSON: Meta exige recibir hub.challenge tal cual, sin envoltorio.
            return result is null
                ? Results.StatusCode(StatusCodes.Status403Forbidden)
                : Results.Text(result);
        });

        // POST — recepción de mensajes/eventos entrantes (ver MANUAL.md sección 3.4).
        endpoints.MapPost(pattern, async (HttpContext context, IArjuyWhatsAppClient whatsAppClient, CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);

            var signatureHeader = context.Request.Headers["X-Hub-Signature-256"].ToString();

            var result = await whatsAppClient.ProcessWebhookAsync(rawBody, signatureHeader, cancellationToken);

            // Meta espera un 200 rápido siempre que la firma sea válida, aunque algún mensaje
            // individual haya fallado río abajo (los handlers ya loguean sus propias excepciones).
            // Solo se responde distinto de 200 cuando ProcessWebhookAsync falló por firma inválida.
            return result.IsSuccess
                ? Results.Ok()
                : Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        return endpoints;
    }
}
