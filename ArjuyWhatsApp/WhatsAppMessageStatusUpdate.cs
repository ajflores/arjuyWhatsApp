namespace ArjuyWhatsApp;

/// <summary>
/// Estado de entrega de un mensaje saliente, tal como lo reporta Meta en
/// <c>entry[].changes[].value.statuses[].status</c>.
/// </summary>
public enum WhatsAppMessageStatus
{
    /// <summary>Meta aceptó el mensaje y lo envió hacia el dispositivo del destinatario.</summary>
    Sent,

    /// <summary>El mensaje llegó al dispositivo del destinatario.</summary>
    Delivered,

    /// <summary>El destinatario leyó el mensaje (doble check azul).</summary>
    Read,

    /// <summary>Meta no pudo entregar el mensaje. Ver <see cref="WhatsAppMessageStatusUpdate.ErrorCode"/> y <see cref="WhatsAppMessageStatusUpdate.ErrorMessage"/>.</summary>
    Failed
}

/// <summary>
/// Representa una actualización de estado de entrega de un mensaje saliente, ya parseada desde
/// el payload crudo del webhook de Meta (<c>entry[].changes[].value.statuses[]</c>), listo para
/// ser consumido por la aplicación sin que esta tenga que conocer la forma del JSON de Meta.
/// </summary>
public class WhatsAppMessageStatusUpdate
{
    /// <summary>Identificador del mensaje saliente al que corresponde este estado (el mismo id devuelto al enviarlo, campo "id").</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Número de teléfono del destinatario del mensaje original, tal como lo manda Meta en el campo "recipient_id" (sin el "+").</summary>
    public string RecipientPhoneNumber { get; set; } = string.Empty;

    /// <summary>Nuevo estado del mensaje.</summary>
    public WhatsAppMessageStatus Status { get; set; }

    /// <summary>Momento en que Meta registra este estado, convertido desde el timestamp Unix (segundos) que manda en el payload.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Código de error devuelto por Meta (campo "code" del primer elemento de "errors") cuando
    /// <see cref="Status"/> es <see cref="WhatsAppMessageStatus.Failed"/>; <c>null</c> en otro caso.
    /// </summary>
    public int? ErrorCode { get; set; }

    /// <summary>
    /// Descripción del error devuelta por Meta (campo "message" del primer elemento de "errors")
    /// cuando <see cref="Status"/> es <see cref="WhatsAppMessageStatus.Failed"/>; <c>null</c> en otro caso.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
