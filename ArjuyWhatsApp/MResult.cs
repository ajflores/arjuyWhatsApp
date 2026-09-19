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

    /// <summary>Constructor protegido: usar siempre las factories estáticas <see cref="Success(string?)"/> y <see cref="Fail(string)"/>.</summary>
    protected MResult(bool isSuccess, string? message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    /// <summary>Crea un resultado exitoso sin dato de retorno.</summary>
    public static MResult Success(string? message = null)
    {
        return new MResult(true, message);
    }

    /// <summary>Crea un resultado fallido con el mensaje de error indicado.</summary>
    public static MResult Fail(string message)
    {
        return new MResult(false, message);
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

    /// <summary>Constructor privado: usar siempre las factories estáticas <see cref="Success(T, string?)"/> y <see cref="Fail(string)"/>.</summary>
    private MResult(bool isSuccess, T? data, string? message)
        : base(isSuccess, message)
    {
        Data = data;
    }

    /// <summary>Crea un resultado exitoso con el dato devuelto por la operación.</summary>
    public static MResult<T> Success(T data, string? message = null)
    {
        return new MResult<T>(true, data, message);
    }

    /// <summary>Crea un resultado fallido con el mensaje de error indicado.</summary>
    public static new MResult<T> Fail(string message)
    {
        return new MResult<T>(false, default, message);
    }
}
