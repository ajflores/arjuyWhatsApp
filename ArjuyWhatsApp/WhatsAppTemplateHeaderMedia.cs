namespace ArjuyWhatsApp;

/// <summary>
/// Tipo de media del header dinámico de una plantilla al enviarla — debe coincidir con el
/// <c>Format</c> ("IMAGE"/"VIDEO"/"DOCUMENT") con el que la plantilla fue aprobada por Meta.
/// </summary>
public enum WhatsAppTemplateHeaderMediaType
{
    /// <summary>Header de imagen.</summary>
    Image,

    /// <summary>Header de video.</summary>
    Video,

    /// <summary>Header de documento — admite <see cref="WhatsAppTemplateHeaderMedia.FileName"/>.</summary>
    Document
}

/// <summary>
/// Media dinámica para el componente <c>header</c> de una plantilla, usada por el overload de
/// <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, WhatsAppTemplateHeaderMedia?, IEnumerable{WhatsAppTemplateButtonParameter}?, string)"/>
/// que soporta header/botones dinámicos. Solo tiene sentido cuando la plantilla fue aprobada por
/// Meta con un header de tipo imagen/video/documento (ver <see cref="WhatsAppTemplateComponent.Format"/>
/// al listarla con <see cref="IArjuyWhatsAppClient.GetMessageTemplatesAsync"/>).
/// </summary>
public class WhatsAppTemplateHeaderMedia
{
    /// <summary>Tipo de media del header.</summary>
    public WhatsAppTemplateHeaderMediaType Type { get; set; }

    /// <summary>
    /// URL pública de la media. Mutuamente excluyente con <see cref="MediaId"/> — exactamente uno de
    /// los dos debe estar seteado.
    /// </summary>
    public string? Link { get; set; }

    /// <summary>
    /// Media id de un archivo subido previamente con <see cref="IArjuyWhatsAppClient.UploadMediaAsync"/>.
    /// Mutuamente excluyente con <see cref="Link"/> — exactamente uno de los dos debe estar seteado.
    /// </summary>
    public string? MediaId { get; set; }

    /// <summary>
    /// Nombre de archivo mostrado al destinatario. Solo aplica (y solo se envía a Meta) cuando
    /// <see cref="Type"/> es <see cref="WhatsAppTemplateHeaderMediaType.Document"/>; se ignora en
    /// header de imagen o video.
    /// </summary>
    public string? FileName { get; set; }
}
