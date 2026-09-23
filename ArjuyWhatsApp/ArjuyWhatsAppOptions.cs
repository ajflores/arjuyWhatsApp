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

    /// <summary>
    /// Código de país ISO 3166-1 alpha-2 (ej. <c>"AR"</c>), usado únicamente para elegir qué
    /// implementación built-in de <see cref="IPhoneNumberNormalizer"/> registrar automáticamente
    /// si el consumidor no registró la suya propia (ver <c>ServiceCollectionExtensions</c>). Los
    /// países sin normalizador built-in (o si se deja <c>null</c>) usan
    /// <see cref="DefaultPhoneNumberNormalizer"/>, que no aplica ninguna transformación además de
    /// recortar espacios. No afecta nada más del comportamiento de la librería.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// Cantidad máxima de reintentos ante errores transitorios de la Graph API de Meta
    /// (<see cref="MetaApiError.IsTransient"/> — rate limiting HTTP 429 o errores 5xx del lado de
    /// Meta). No cuenta el intento inicial: con el valor por defecto <c>3</c>, una operación hace
    /// como máximo 4 llamados HTTP en total (1 inicial + 3 reintentos). <c>0</c> desactiva el
    /// retry por completo — la librería se comporta como antes, devolviendo el primer error tal
    /// cual. Errores no transitorios (400, 401, 403, validaciones locales) nunca se reintentan,
    /// sin importar este valor.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay base del backoff exponencial entre reintentos. El reintento N espera
    /// <c>BaseRetryDelay * 2^(N-1)</c> (con jitter aleatorio de ±20% para evitar reintentos
    /// sincronizados entre múltiples instancias) — por ejemplo, con el default de <c>500ms</c>: el
    /// primer reintento espera ~500ms, el segundo ~1s, el tercero ~2s. Si la respuesta 429 de Meta
    /// trae el header <c>Retry-After</c>, ese valor se usa en su lugar (Meta indica explícitamente
    /// cuánto esperar). Sin efecto si <see cref="MaxRetryAttempts"/> es <c>0</c>.
    /// </summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
