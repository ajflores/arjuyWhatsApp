namespace ArjuyWhatsApp;

/// <summary>
/// Tipo de contenido de un <see cref="WhatsAppMessageReceived"/>. Solo cubre los tipos que la
/// librería sabe interpretar hoy; cualquier otro tipo que mande Meta se mapea a
/// <see cref="Unknown"/> y se sigue entregando el mensaje (con <see cref="WhatsAppMessageReceived.Text"/>
/// y <see cref="WhatsAppMessageReceived.MediaId"/> en <c>null</c>) para que el consumidor decida qué hacer.
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
    Interactive,

    /// <summary>Ubicación. Los datos quedan en <see cref="WhatsAppMessageReceived.Location"/>.</summary>
    Location,

    /// <summary>Una o más tarjetas de contacto. Quedan en <see cref="WhatsAppMessageReceived.Contacts"/>.</summary>
    Contacts,

    /// <summary>
    /// Reacción con emoji a un mensaje enviado previamente. El emoji y el id del mensaje reaccionado
    /// quedan en <see cref="WhatsAppMessageReceived.ReactionEmoji"/> y
    /// <see cref="WhatsAppMessageReceived.ReactionToMessageId"/>. Un <see cref="WhatsAppMessageReceived.ReactionEmoji"/>
    /// vacío significa que el remitente removió una reacción puesta anteriormente.
    /// </summary>
    Reaction,

    /// <summary>
    /// Nota de audio o mensaje de voz. El binario se descarga aparte con
    /// <see cref="IArjuyWhatsAppClient.DownloadMediaAsync"/> usando <see cref="WhatsAppMessageReceived.MediaId"/>.
    /// <see cref="WhatsAppMessageReceived.IsVoiceNote"/> distingue una nota de voz grabada en el chat
    /// de un archivo de audio compartido.
    /// </summary>
    Audio,

    /// <summary>Video. El binario se descarga aparte con <see cref="IArjuyWhatsAppClient.DownloadMediaAsync"/> usando <see cref="WhatsAppMessageReceived.MediaId"/>.</summary>
    Video,

    /// <summary>Sticker (WebP). El binario se descarga aparte con <see cref="IArjuyWhatsAppClient.DownloadMediaAsync"/> usando <see cref="WhatsAppMessageReceived.MediaId"/>.</summary>
    Sticker
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
    /// Media id asignado por Meta cuando <see cref="Type"/> es <see cref="WhatsAppMessageType.Image"/>,
    /// <see cref="WhatsAppMessageType.Document"/>, <see cref="WhatsAppMessageType.Audio"/>,
    /// <see cref="WhatsAppMessageType.Video"/> o <see cref="WhatsAppMessageType.Sticker"/>; <c>null</c>
    /// en otro caso. Usar con <see cref="IArjuyWhatsAppClient.DownloadMediaAsync"/> para obtener el binario.
    /// </summary>
    public string? MediaId { get; set; }

    /// <summary>
    /// <c>true</c> si el audio recibido es una nota de voz grabada en el chat (vs. un archivo de
    /// audio compartido), <c>false</c> si no lo es, <c>null</c> si <see cref="Type"/> no es
    /// <see cref="WhatsAppMessageType.Audio"/>.
    /// </summary>
    public bool? IsVoiceNote { get; set; }

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

    /// <summary>Ubicación recibida cuando <see cref="Type"/> es <see cref="WhatsAppMessageType.Location"/>; <c>null</c> en otro caso.</summary>
    public WhatsAppReceivedLocation? Location { get; set; }

    /// <summary>Contacto(s) recibidos cuando <see cref="Type"/> es <see cref="WhatsAppMessageType.Contacts"/>; lista vacía en otro caso.</summary>
    public List<WhatsAppContact> Contacts { get; set; } = [];

    /// <summary>
    /// Emoji de la reacción cuando <see cref="Type"/> es <see cref="WhatsAppMessageType.Reaction"/>;
    /// <c>null</c> en otro caso. Vacío (no <c>null</c>) significa que el remitente removió una
    /// reacción puesta anteriormente.
    /// </summary>
    public string? ReactionEmoji { get; set; }

    /// <summary>Id (<c>wamid.</c>) del mensaje al que se reaccionó, cuando <see cref="Type"/> es <see cref="WhatsAppMessageType.Reaction"/>; <c>null</c> en otro caso.</summary>
    public string? ReactionToMessageId { get; set; }
}

/// <summary>Ubicación recibida en un <see cref="WhatsAppMessageReceived"/>.</summary>
public class WhatsAppReceivedLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
}
