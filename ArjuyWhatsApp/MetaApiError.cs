namespace ArjuyWhatsApp;

/// <summary>
/// Error estructurado devuelto por la Graph API de Meta (WhatsApp Cloud API), parseado del
/// objeto <c>error</c> que Meta incluye en el body de una respuesta no exitosa:
/// <c>{"error":{"message":"...","type":"...","code":131026,"error_subcode":2494010,"fbtrace_id":"..."}}</c>.
/// Permite al consumidor de la librería distinguir programáticamente distintas causas de fallo
/// (número sin WhatsApp, token vencido, rate limiting, fuera de ventana de 24hs, etc.) en vez de
/// tener que parsear un string libre.
/// </summary>
public class MetaApiError
{
    /// <summary>Código HTTP de la respuesta (ej. 400, 401, 429, 500).</summary>
    public int HttpStatusCode { get; }

    /// <summary>Campo <c>error.code</c> de Meta (código de error de la plataforma), si el body pudo parsearse.</summary>
    public int? Code { get; }

    /// <summary>Campo <c>error.error_subcode</c> de Meta, si vino presente.</summary>
    public int? ErrorSubcode { get; }

    /// <summary>Campo <c>error.type</c> de Meta (ej. <c>"OAuthException"</c>), si vino presente.</summary>
    public string? Type { get; }

    /// <summary>
    /// Mensaje descriptivo del error. Es <c>error.message</c> cuando el body pudo parsearse como el
    /// formato esperado de Meta; si no, es el body crudo de la respuesta (para no perder información
    /// aunque no tenga la forma esperada).
    /// </summary>
    public string Message { get; }

    /// <summary>Campo <c>error.fbtrace_id</c> de Meta, útil para reportar el error a soporte de Meta.</summary>
    public string? FbTraceId { get; }

    /// <summary>Body crudo tal cual lo devolvió Meta, por si el consumidor necesita algún campo no mapeado acá.</summary>
    public string RawBody { get; }

    public MetaApiError(int httpStatusCode, string rawBody, string message, int? code = null, int? errorSubcode = null, string? type = null, string? fbTraceId = null)
    {
        HttpStatusCode = httpStatusCode;
        RawBody = rawBody;
        Message = message;
        Code = code;
        ErrorSubcode = errorSubcode;
        Type = type;
        FbTraceId = fbTraceId;
    }

    /// <summary>
    /// Indica si el error corresponde a rate limiting de Meta. Hoy se basa únicamente en el status
    /// HTTP 429 — Meta documenta varios <c>code</c> asociados a límites (ej. <c>4</c> "Application
    /// request limit reached", <c>80007</c> "WhatsApp Business Account rate limit reached", <c>130429</c>
    /// "Rate limit hit"), pero no hay una lista cerrada y oficial de todos los codes posibles, así que
    /// para no dar falsos negativos/positivos se prioriza el status HTTP, que Meta usa de forma
    /// consistente para rate limiting. Se complementa igual con los codes conocidos.
    /// </summary>
    public bool IsRateLimited =>
        HttpStatusCode == 429 ||
        Code is 4 or 80007 or 130429 or 131056;

    /// <summary>
    /// Indica si el error es candidato razonable a reintentar (rate limiting o error transitorio del
    /// lado de Meta, HTTP 5xx). No incluye errores de configuración/autenticación (401/403) ni de
    /// validación (400) — esos no se van a resolver reintentando sin cambiar la request.
    /// </summary>
    public bool IsTransient => IsRateLimited || HttpStatusCode >= 500;
}
