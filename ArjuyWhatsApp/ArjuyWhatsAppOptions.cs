namespace ArjuyWhatsApp;

/// <summary>
/// Opciones de configuración para el cliente de WhatsApp Cloud API de Meta.
/// Se puede registrar bindeando una sección de <c>IConfiguration</c> o
/// configurando los valores directamente en código (ver <c>ServiceCollectionExtensions</c>).
/// </summary>
public class ArjuyWhatsAppOptions
{
    /// <summary>Token de acceso (permanente o temporal) de la app de Meta con permisos de WhatsApp Business.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Identificador del número de teléfono de WhatsApp Business (Phone Number ID) provisto por Meta.</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Identificador de la cuenta de WhatsApp Business (WABA ID), usado para operaciones como listar plantillas.</summary>
    public string BusinessAccountId { get; set; } = string.Empty;

    /// <summary>Secreto de la app de Meta, usado para validar la firma HMAC de los webhooks entrantes (ver Roadmap en el README).</summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>Token de verificación configurado en el panel de Meta para el challenge de suscripción del webhook (ver Roadmap en el README).</summary>
    public string VerifyToken { get; set; } = string.Empty;

    /// <summary>Versión de la Graph API de Meta contra la que se realizan los llamados. Por defecto <c>"v21.0"</c>.</summary>
    public string ApiVersion { get; set; } = "v21.0";
}
