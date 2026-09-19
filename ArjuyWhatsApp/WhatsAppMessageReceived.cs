namespace ArjuyWhatsApp;

/// <summary>
/// Tipo de contenido de un <see cref="WhatsAppMessageReceived"/>. Solo cubre los tipos que la
/// librería sabe interpretar hoy; cualquier otro tipo que mande Meta (location, sticker, reaction,
/// contacts, etc.) se mapea a <see cref="Unknown"/> y se sigue entregando el mensaje (con
/// <see cref="WhatsAppMessageReceived.Text"/> y <see cref="WhatsAppMessageReceived.MediaId"/> en
/// <c>null</c>) para que el consumidor decida qué hacer.
/// </summary>
public enum WhatsAppMessageType
{
    /// <summary>Tipo no reconocido/soportado explícitamente por la librería.</summary>
    Unknown = 0,

    /// <summary>Mensaje de texto libre.</summary>
    Text,

    /// <summary>Imagen. El binario se descarga aparte con <see cref="IArjuyWhatsAppClient.DownloadMediaAsync"/> usando <see cref="WhatsAppMessageReceived.MediaId"/>.</summary>
    Image,

    /// <summary>Documento. El binario se descarga aparte con <see cref="IArjuyWhatsAppClient.DownloadMediaAsync"/> usando <see cref="WhatsAppMessageReceived.MediaId"/>.</summary>
    Document,

    /// <summary>
    /// Respuesta a un mensaje interactivo (botón de respuesta rápida o fila de una lista) enviado
    /// previamente con <see cref="IArjuyWhatsAppClient.SendInteractiveButtonsAsync"/> o
    /// <see cref="IArjuyWhatsAppClient.SendInteractiveListAsync"/>. El id/título elegido queda en
    /// <see cref="WhatsAppMessageReceived.InteractiveReplyId"/> y
    /// <see cref="WhatsAppMessageReceived.InteractiveReplyTitle"/>.
    /// </summary>
    Interactive
}

/// <summary>
/// Representa un mensaje entrante de WhatsApp ya parseado desde el payload crudo del webhook de
/// Meta (<c>entry[].changes[].value.messages[]</c>), listo para ser consumido por la aplicación
/// sin que esta tenga que conocer la forma del JSON de Meta.
/// </summary>
public class WhatsAppMessageReceived
{
    /// <summary>Número de teléfono del remitente, tal como lo manda Meta en el campo "from" (sin el "+").</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Identificador único del mensaje asignado por Meta (campo "id" del mensaje entrante).</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Tipo de contenido del mensaje.</summary>
    public WhatsAppMessageType Type { get; set; } = WhatsAppMessageType.Unknown;

    /// <summary>Cuerpo del mensaje de texto cuando <see cref="Type"/> es <see cref="WhatsAppMessageType.Text"/>; <c>null</c> en otro caso.</summary>
    public string? Text { get; set; }

    /// <summary>
    /// Media id asignado por Meta cuando <see cref="Type"/> es <see cref="WhatsAppMessageType.Image"/>
    /// o <see cref="WhatsAppMessageType.Document"/>; <c>null</c> en otro caso. Usar con
    /// <see cref="IArjuyWhatsAppClient.DownloadMediaAsync"/> para obtener el binario.
    /// </summary>
    public string? MediaId { get; set; }

    /// <summary>Momento en que Meta registra el mensaje, convertido desde el timestamp Unix (segundos) que manda en el payload.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Id de la opción elegida (<c>button_reply.id</c> o <c>list_reply.id</c>) cuando
    /// <see cref="Type"/> es <see cref="WhatsAppMessageType.Interactive"/>; <c>null</c> en otro caso.
    /// Es el mismo <c>Id</c> que se pasó al armar los botones/filas con
    /// <see cref="IArjuyWhatsAppClient.SendInteractiveButtonsAsync"/> o
    /// <see cref="IArjuyWhatsAppClient.SendInteractiveListAsync"/> — pensado para que el consumidor
    /// lo use como clave de decisión en su lógica de negocio.
    /// </summary>
    public string? InteractiveReplyId { get; set; }

    /// <summary>
    /// Título de la opción elegida (<c>button_reply.title</c> o <c>list_reply.title</c>) cuando
    /// <see cref="Type"/> es <see cref="WhatsAppMessageType.Interactive"/>; <c>null</c> en otro caso.
    /// Pensado solo para mostrar/loguear — para la lógica de negocio usar <see cref="InteractiveReplyId"/>.
    /// </summary>
    public string? InteractiveReplyTitle { get; set; }
}
