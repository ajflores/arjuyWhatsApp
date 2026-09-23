namespace ArjuyWhatsApp;

/// <summary>
/// Resultado de una operación sin dato de retorno. Contrato propio y autocontenido
/// (no depende de ningún paquete de terceros), pensado para exponer éxito/fracaso
/// de forma ergonómica sin recurrir a excepciones para el flujo normal.
/// </summary>
public class MResult
{
    /// <summary>Indica si la operación fue exitosa.</summary>
    public bool IsSuccess { get; }

    /// <summary>Mensaje descriptivo (motivo de error, o mensaje informativo en éxito).</summary>
    public string? Message { get; }

    /// <summary>
    /// Error estructurado devuelto por Meta cuando el fallo vino de una respuesta de la Graph API
    /// (rate limiting, token inválido, número sin WhatsApp, etc.). Es <c>null</c> cuando el resultado
    /// es exitoso, o cuando el fallo es de validación local de la librería (por ejemplo límites de
    /// botones/listas o configuración faltante) — esos casos no tienen un error de Meta detrás y
    /// siguen usando <see cref="Fail(string)"/>.
    /// </summary>
    public MetaApiError? Error { get; }

    /// <summary>Constructor protegido: usar siempre las factories estáticas <see cref="Success(string?)"/>, <see cref="Fail(string)"/> y <see cref="Fail(MetaApiError)"/>.</summary>
    protected MResult(bool isSuccess, string? message, MetaApiError? error = null)
    {
        IsSuccess = isSuccess;
        Message = message;
        Error = error;
    }

    /// <summary>Crea un resultado exitoso sin dato de retorno.</summary>
    public static MResult Success(string? message = null)
    {
        return new MResult(true, message);
    }

    /// <summary>Crea un resultado fallido con el mensaje de error indicado. Para errores devueltos por Meta, preferir <see cref="Fail(MetaApiError)"/>.</summary>
    public static MResult Fail(string message)
    {
        return new MResult(false, message);
    }

    /// <summary>Crea un resultado fallido a partir de un error estructurado de Meta. <see cref="Message"/> queda poblado con <see cref="MetaApiError.Message"/> para no romper a consumidores que solo leen el mensaje.</summary>
    public static MResult Fail(MetaApiError error)
    {
        return new MResult(false, error.Message, error);
    }
}

/// <summary>
/// Resultado genérico de una operación, con el dato devuelto en caso de éxito.
/// Mismo contrato que <see cref="MResult"/> pero transportando un valor <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Tipo del dato devuelto por la operación exitosa.</typeparam>
public class MResult<T> : MResult
{
    /// <summary>Dato devuelto por la operación cuando <see cref="MResult.IsSuccess"/> es <c>true</c>. Puede ser el valor por defecto de <typeparamref name="T"/> en caso de fallo.</summary>
    public T? Data { get; }

    /// <summary>Constructor privado: usar siempre las factories estáticas <see cref="Success(T, string?)"/>, <see cref="Fail(string)"/> y <see cref="Fail(MetaApiError)"/>.</summary>
    private MResult(bool isSuccess, T? data, string? message, MetaApiError? error = null)
        : base(isSuccess, message, error)
    {
        Data = data;
    }

    /// <summary>Crea un resultado exitoso con el dato devuelto por la operación.</summary>
    public static MResult<T> Success(T data, string? message = null)
    {
        return new MResult<T>(true, data, message);
    }

    /// <summary>Crea un resultado fallido con el mensaje de error indicado. Para errores devueltos por Meta, preferir <see cref="Fail(MetaApiError)"/>.</summary>
    public static new MResult<T> Fail(string message)
    {
        return new MResult<T>(false, default, message);
    }

    /// <summary>Crea un resultado fallido a partir de un error estructurado de Meta. <see cref="MResult.Message"/> queda poblado con <see cref="MetaApiError.Message"/> para no romper a consumidores que solo leen el mensaje.</summary>
    public static new MResult<T> Fail(MetaApiError error)
    {
        return new MResult<T>(false, default, error.Message, error);
    }
}
