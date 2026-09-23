namespace ArjuyWhatsApp;

/// <summary>
/// Estado de aprobación de una plantilla de mensaje ante Meta. Ver
/// <see href="https://developers.facebook.com/docs/whatsapp/business-management-api/message-templates">
/// documentación oficial de Message Templates</see>.
/// </summary>
public enum WhatsAppMessageTemplateStatus
{
    /// <summary>Estado no reconocido por la librería — Meta puede agregar valores nuevos en el futuro.</summary>
    Unknown = 0,

    /// <summary>Plantilla aprobada, lista para usarse con <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, string)"/>.</summary>
    Approved,

    /// <summary>Plantilla enviada a revisión, todavía no aprobada.</summary>
    Pending,

    /// <summary>Plantilla rechazada por Meta. Ver <see cref="WhatsAppMessageTemplate.RejectedReason"/>.</summary>
    Rejected,

    /// <summary>Plantilla pausada temporalmente por baja calidad (feedback negativo de destinatarios).</summary>
    Paused,

    /// <summary>Plantilla deshabilitada de forma permanente tras pausas repetidas.</summary>
    Disabled
}

/// <summary>
/// Tipo de un componente de plantilla (<c>components[].type</c> en la respuesta de Meta).
/// </summary>
public enum WhatsAppTemplateComponentType
{
    /// <summary>Tipo no reconocido por la librería.</summary>
    Unknown = 0,

    /// <summary>Encabezado — texto, imagen, video o documento, según <see cref="WhatsAppTemplateComponent.Format"/>.</summary>
    Header,

    /// <summary>Cuerpo principal del mensaje — el componente que acepta los parámetros posicionales de <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, string)"/>.</summary>
    Body,

    /// <summary>Pie de página, texto fijo sin parámetros.</summary>
    Footer,

    /// <summary>Botones (respuesta rápida, URL o llamada telefónica) — ver <see cref="WhatsAppTemplateComponent.Buttons"/>.</summary>
    Buttons
}

/// <summary>
/// Un botón declarado en un componente <see cref="WhatsAppTemplateComponentType.Buttons"/> de una
/// plantilla. Representa la definición aprobada por Meta (texto y, si aplica, URL/teléfono fijos o
/// con placeholder <c>{{1}}</c>) — no el botón que efectivamente se envía (eso lo arma Meta a partir
/// de esta definición al momento de enviar el template).
/// </summary>
public class WhatsAppTemplateButton
{
    /// <summary>Tipo de botón tal como lo manda Meta (ej. <c>"QUICK_REPLY"</c>, <c>"URL"</c>, <c>"PHONE_NUMBER"</c>).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Texto visible del botón.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>URL del botón cuando <see cref="Type"/> es <c>"URL"</c> (puede contener un placeholder <c>{{1}}</c> si es dinámica); <c>null</c> en otro caso.</summary>
    public string? Url { get; set; }

    /// <summary>Número de teléfono del botón cuando <see cref="Type"/> es <c>"PHONE_NUMBER"</c>; <c>null</c> en otro caso.</summary>
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Un componente de una plantilla (header, body, footer o buttons), tal como lo devuelve Meta al
/// listar plantillas. No es lo mismo que un componente de <em>envío</em> de template (los parámetros
/// que arma <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, string)"/>) — este es el componente de
/// <em>definición</em>, útil para saber qué parámetros pide la plantilla antes de enviarla.
/// </summary>
public class WhatsAppTemplateComponent
{
    /// <summary>Tipo de componente.</summary>
    public WhatsAppTemplateComponentType Type { get; set; } = WhatsAppTemplateComponentType.Unknown;

    /// <summary>
    /// Formato del header (<c>"TEXT"</c>, <c>"IMAGE"</c>, <c>"VIDEO"</c>, <c>"DOCUMENT"</c>,
    /// <c>"LOCATION"</c>) cuando <see cref="Type"/> es <see cref="WhatsAppTemplateComponentType.Header"/>;
    /// <c>null</c> en otro caso.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Texto del componente (header de texto, body o footer), con placeholders <c>{{1}}</c>,
    /// <c>{{2}}</c>, etc. donde corresponda; <c>null</c> cuando el componente no tiene texto (por
    /// ejemplo un header de imagen, o un componente de tipo <see cref="WhatsAppTemplateComponentType.Buttons"/>).
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Cantidad de placeholders posicionales (<c>{{1}}</c>, <c>{{2}}</c>, ...) que contiene
    /// <see cref="Text"/> — pensado para que el consumidor sepa cuántos <c>parameters</c> pasarle a
    /// <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, string)"/> sin tener que contarlos a mano. <c>0</c>
    /// si <see cref="Text"/> es <c>null</c> o no tiene placeholders.
    /// </summary>
    public int ParameterCount { get; set; }

    /// <summary>Botones del componente cuando <see cref="Type"/> es <see cref="WhatsAppTemplateComponentType.Buttons"/>; lista vacía en otro caso.</summary>
    public IReadOnlyList<WhatsAppTemplateButton> Buttons { get; set; } = Array.Empty<WhatsAppTemplateButton>();
}

/// <summary>
/// Una plantilla de mensaje de WhatsApp Business tal como la devuelve
/// <see cref="IArjuyWhatsAppClient.GetMessageTemplatesAsync"/>. Representa la plantilla aprobada (o
/// en otro estado) contra la Business Management API de Meta — no confundir con el payload que arma
/// <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, string)"/> para enviarla.
/// </summary>
public class WhatsAppMessageTemplate
{
    /// <summary>Identificador de la plantilla asignado por Meta.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Nombre de la plantilla — el mismo valor que se pasa como <c>templateName</c> a <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, string)"/>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Código de idioma de la plantilla (ej. <c>"es"</c>, <c>"es_AR"</c>) — el mismo valor que se pasa como <c>languageCode</c> a <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, string)"/>.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Estado de aprobación de la plantilla.</summary>
    public WhatsAppMessageTemplateStatus Status { get; set; } = WhatsAppMessageTemplateStatus.Unknown;

    /// <summary>Categoría asignada por Meta (ej. <c>"MARKETING"</c>, <c>"UTILITY"</c>, <c>"AUTHENTICATION"</c>).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Motivo de rechazo cuando <see cref="Status"/> es <see cref="WhatsAppMessageTemplateStatus.Rejected"/>; <c>null</c> en otro caso.</summary>
    public string? RejectedReason { get; set; }

    /// <summary>Componentes de la plantilla (header/body/footer/buttons), en el orden en que los devuelve Meta.</summary>
    public IReadOnlyList<WhatsAppTemplateComponent> Components { get; set; } = Array.Empty<WhatsAppTemplateComponent>();

    /// <summary>
    /// Cantidad de parámetros posicionales que pide el componente <see cref="WhatsAppTemplateComponentType.Body"/>
    /// de esta plantilla — atajo equivalente a buscar ese componente en <see cref="Components"/> y leer
    /// su <see cref="WhatsAppTemplateComponent.ParameterCount"/>. <c>0</c> si la plantilla no tiene body
    /// con placeholders (no debería pasar en la práctica, Meta exige un body).
    /// </summary>
    public int BodyParameterCount =>
        Components.FirstOrDefault(c => c.Type == WhatsAppTemplateComponentType.Body)?.ParameterCount ?? 0;
}
