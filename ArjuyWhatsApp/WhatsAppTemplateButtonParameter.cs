namespace ArjuyWhatsApp;

/// <summary>
/// Sub-tipo de un botón dinámico de plantilla al enviarla (<c>components[].sub_type</c> en el
/// payload de envío) — debe coincidir con el tipo de botón con el que la plantilla fue aprobada
/// por Meta (<see cref="WhatsAppTemplateButton.Type"/> al listarla).
/// </summary>
public enum WhatsAppTemplateButtonSubType
{
    /// <summary>
    /// Botón de URL dinámica — el botón fue aprobado con una URL que contiene un placeholder
    /// <c>{{1}}</c> (ej. <c>"https://tusitio.com/pedido/{{1}}"</c>), y <see cref="WhatsAppTemplateButtonParameter.Value"/>
    /// es el valor que reemplaza ese placeholder.
    /// </summary>
    Url,

    /// <summary>
    /// Botón de respuesta rápida — <see cref="WhatsAppTemplateButtonParameter.Value"/> es el
    /// <c>payload</c> que Meta va a devolver en el mensaje entrante cuando el destinatario lo toque.
    /// </summary>
    QuickReply
}

/// <summary>
/// Valor dinámico para un botón de una plantilla al enviarla, usado por el overload de
/// <see cref="IArjuyWhatsAppClient.SendTemplateAsync(string, string, string, IEnumerable{string}, WhatsAppTemplateHeaderMedia?, IEnumerable{WhatsAppTemplateButtonParameter}?, string)"/>
/// que soporta header/botones dinámicos. Solo hace falta pasar uno por cada botón de la plantilla
/// que efectivamente tenga un placeholder dinámico — un <c>QUICK_REPLY</c> con payload fijo (sin
/// <c>{{1}}</c>) no necesita parámetro.
/// </summary>
public class WhatsAppTemplateButtonParameter
{
    /// <summary>
    /// Posición (0-indexada) del botón dentro del componente <c>BUTTONS</c> de la plantilla, en el
    /// mismo orden en que Meta los devuelve en <see cref="WhatsAppTemplateComponent.Buttons"/>.
    /// </summary>
    public int Index { get; set; }

    /// <summary>Sub-tipo del botón, debe coincidir con el tipo aprobado en la plantilla.</summary>
    public WhatsAppTemplateButtonSubType SubType { get; set; }

    /// <summary>
    /// Valor dinámico: el sufijo de URL cuando <see cref="SubType"/> es <see cref="WhatsAppTemplateButtonSubType.Url"/>,
    /// o el payload cuando es <see cref="WhatsAppTemplateButtonSubType.QuickReply"/>.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
