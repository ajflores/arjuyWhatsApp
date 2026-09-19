namespace ArjuyWhatsApp;

/// <summary>
/// Contrato para manejar mensajes entrantes de WhatsApp vía inyección de dependencias.
/// Registrá una o más implementaciones en el contenedor de DI (típicamente como <c>Scoped</c>) y
/// <see cref="IArjuyWhatsAppClient.ProcessWebhookAsync"/> las va a resolver e invocar por cada
/// mensaje recibido, además de disparar el evento <see cref="IArjuyWhatsAppClient.MessageReceived"/>.
/// Ambos mecanismos (evento y DI) conviven: usá el que mejor encaje con tu escenario (el evento
/// para hooks livianos/ad-hoc, los handlers de DI cuando necesitás resolver otros servicios
/// scoped, como un <c>DbContext</c>).
/// </summary>
public interface IWhatsAppMessageHandler
{
    /// <summary>
    /// Procesa un mensaje entrante de WhatsApp. Una excepción no controlada acá es capturada y
    /// logueada por el procesador del webhook — no interrumpe el procesamiento de otros handlers
    /// ni del resto del webhook.
    /// </summary>
    /// <param name="message">Mensaje entrante ya parseado.</param>
    /// <param name="cancellationToken">Token de cancelación de la request del webhook.</param>
    Task HandleAsync(WhatsAppMessageReceived message, CancellationToken cancellationToken = default);
}
