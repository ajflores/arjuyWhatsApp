namespace ArjuyWhatsApp;

/// <summary>
/// Un contacto a enviar (o recibido) como mensaje de tipo <c>contacts</c> — equivalente a una
/// vCard simplificada. Formato verificado contra la documentación oficial de Meta (WhatsApp Cloud
/// API, "Contacts Messages") al 2026-09-19. Solo <see cref="Name"/> con
/// <see cref="WhatsAppContactName.FormattedName"/> es obligatorio; el resto de las secciones son
/// opcionales y se omiten del payload de envío si vienen en <c>null</c>/vacías.
/// </summary>
public class WhatsAppContact
{
    /// <summary>Nombre del contacto. <see cref="WhatsAppContactName.FormattedName"/> es el único campo obligatorio de todo el contacto.</summary>
    public WhatsAppContactName Name { get; set; } = new();

    /// <summary>Teléfonos del contacto (opcional, puede ser vacío).</summary>
    public List<WhatsAppContactPhone> Phones { get; set; } = [];

    /// <summary>Emails del contacto (opcional, puede ser vacío).</summary>
    public List<WhatsAppContactEmail> Emails { get; set; } = [];

    /// <summary>Direcciones del contacto (opcional, puede ser vacío).</summary>
    public List<WhatsAppContactAddress> Addresses { get; set; } = [];

    /// <summary>Datos laborales del contacto (opcional).</summary>
    public WhatsAppContactOrg? Org { get; set; }

    /// <summary>Fecha de nacimiento en formato <c>"YYYY-MM-DD"</c> (opcional).</summary>
    public string? Birthday { get; set; }

    /// <summary>Sitios web del contacto (opcional, puede ser vacío).</summary>
    public List<WhatsAppContactUrl> Urls { get; set; } = [];
}

/// <summary>Nombre de un <see cref="WhatsAppContact"/>. Solo <see cref="FormattedName"/> es obligatorio para Meta.</summary>
public class WhatsAppContactName
{
    /// <summary>Nombre completo tal como se muestra — el único campo de todo <see cref="WhatsAppContact"/> que Meta exige.</summary>
    public string FormattedName { get; set; } = string.Empty;

    /// <summary>Nombre de pila (opcional).</summary>
    public string? FirstName { get; set; }

    /// <summary>Apellido (opcional).</summary>
    public string? LastName { get; set; }

    /// <summary>Segundo nombre (opcional).</summary>
    public string? MiddleName { get; set; }

    /// <summary>Sufijo, ej. "Jr." (opcional).</summary>
    public string? Suffix { get; set; }

    /// <summary>Prefijo, ej. "Sr." (opcional).</summary>
    public string? Prefix { get; set; }
}

/// <summary>Teléfono de un <see cref="WhatsAppContact"/>.</summary>
public class WhatsAppContactPhone
{
    /// <summary>Número de teléfono, en el formato que se quiera mostrar (no necesariamente el que exige el envío de mensajes).</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Etiqueta libre, ej. <c>"WORK"</c>, <c>"HOME"</c>, <c>"CELL"</c> (opcional).</summary>
    public string? Type { get; set; }

    /// <summary>
    /// Id de WhatsApp asociado a este teléfono (sin "+"), si se sabe que tiene WhatsApp — Meta lo usa
    /// para ofrecer "agregar a contactos" con un enlace directo al chat (opcional).
    /// </summary>
    public string? WaId { get; set; }
}

/// <summary>Email de un <see cref="WhatsAppContact"/>.</summary>
public class WhatsAppContactEmail
{
    /// <summary>Dirección de email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Etiqueta libre, ej. <c>"WORK"</c>, <c>"HOME"</c> (opcional).</summary>
    public string? Type { get; set; }
}

/// <summary>Dirección postal de un <see cref="WhatsAppContact"/>. Todos los campos son opcionales.</summary>
public class WhatsAppContactAddress
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }

    /// <summary>Etiqueta libre, ej. <c>"WORK"</c>, <c>"HOME"</c> (opcional).</summary>
    public string? Type { get; set; }
}

/// <summary>Datos laborales de un <see cref="WhatsAppContact"/>. Todos los campos son opcionales.</summary>
public class WhatsAppContactOrg
{
    public string? Company { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
}

/// <summary>Sitio web de un <see cref="WhatsAppContact"/>.</summary>
public class WhatsAppContactUrl
{
    /// <summary>Dirección del sitio web.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Etiqueta libre, ej. <c>"WORK"</c>, <c>"HOME"</c> (opcional).</summary>
    public string? Type { get; set; }
}
