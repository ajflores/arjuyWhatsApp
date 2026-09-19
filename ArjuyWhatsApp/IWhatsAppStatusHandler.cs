namespace ArjuyWhatsApp;

/// <summary>
/// Contrato para manejar actualizaciones de estado de entrega de mensajes salientes de WhatsApp
/// (sent/delivered/read/failed) vía inyección de dependencias. Registrá una o más implementaciones
/// en el contenedor de DI (típicamente como <c>Scoped</c>) y
/// <see cref="IArjuyWhatsAppClient.ProcessWebhookAsync"/> las va a resolver e invocar por cada
/// actualización de estado recibida, además de disparar el evento
/// <see cref="IArjuyWhatsAppClient.MessageStatusUpdated"/>. Es un contrato separado de
/// <see cref="IWhatsAppMessageHandler"/> a propósito: un mensaje entrante y un estado de entrega de
/// un mensaje saliente son eventos de dominio distintos (payloads distintos, momentos distintos,
/// consumidores típicamente distintos — por ejemplo, actualizar el estado de una notificación ya
/// enviada no tiene nada que ver con procesar una respuesta del usuario) y forzarlos a compartir
/// una sola interfaz obligaría a todo handler a chequear de qué caso se trata con un <c>if</c>.
/// Ambos mecanismos (evento y DI) conviven: usá el que mejor encaje con tu escenario.
/// </summary>
public interface IWhatsAppStatusHandler
{
    /// <summary>
    /// Procesa una actualización de estado de entrega de un mensaje saliente. Una excepción no
    /// controlada acá es capturada y logueada por el procesador del webhook — no interrumpe el
    /// procesamiento de otros handlers ni del resto del webhook.
    /// </summary>
    /// <param name="status">Actualización de estado ya parseada.</param>
    /// <param name="cancellationToken">Token de cancelación de la request del webhook.</param>
    Task HandleAsync(WhatsAppMessageStatusUpdate status, CancellationToken cancellationToken = default);
}
