namespace ArjuyWhatsApp;

/// <summary>
/// Normalizador de números de teléfono argentinos al formato exacto que exige la WhatsApp Cloud
/// API de Meta para celulares: código de país <c>54</c> + <c>9</c> + número nacional significativo
/// de 10 dígitos (código de área + abonado, sin el <c>0</c> de prefijo troncal ni el <c>15</c> de
/// marcado local de celular). Ejemplo: un número que un usuario podría escribir como
/// <c>"011 15-1234-5678"</c> (CABA, formato local) se normaliza a <c>"5491123456781"</c> — el
/// <c>9</c> va INMEDIATAMENTE DESPUÉS del código de país, nunca al final ni reemplazando al
/// <c>15</c> en su lugar original.
/// </summary>
/// <remarks>
/// <para>
/// Esta clase asume que TODO número que recibe es un celular (WhatsApp Business API solo tiene
/// sentido para números que efectivamente tienen WhatsApp, que en la práctica son casi siempre
/// celulares) — no intenta distinguir celular de línea fija, así que si te llega un fijo le va a
/// agregar el <c>9</c> igual, lo cual sería incorrecto para ese caso puntual. No es un problema
/// esperable en el uso normal de la librería.
/// </para>
/// <para>
/// <b>Limitación conocida — remoción del "15" local:</b> Argentina no tiene una regla fija de
/// longitud para el código de área (varía entre 2 dígitos, ej. <c>11</c> para CABA/GBA, y 3-4
/// dígitos para el resto del país), y no existe en esta clase una tabla completa de códigos de
/// área de ENACOM. Cuando el número recibido tiene la forma "área + 15 + abonado" (12 dígitos
/// después de sacar el <c>0</c> troncal y el código de país), esta implementación prueba, en
/// orden, si el <c>"15"</c> aparece justo después de un código de área de 2, 3 o 4 dígitos, y usa
/// el primer calce. Para CABA/GBA (código de área <c>11</c>, el caso más común con diferencia)
/// esto es exacto. Para el resto del país puede haber falsos positivos si el número del abonado
/// contiene la subcadena <c>"15"</c> en una posición que coincide con un código de área de 3 o 4
/// dígitos válido — en ese caso el resultado puede quedar mal armado. Si tu aplicación opera
/// mayormente fuera de CABA/GBA y necesitás precisión total, registrá tu propio
/// <see cref="IPhoneNumberNormalizer"/> con la tabla de áreas que corresponda a tu operación.
/// </para>
/// </remarks>
public class ArgentinaPhoneNumberNormalizer : IPhoneNumberNormalizer
{
    private const string CountryCode = "54";
    private const string MobilePrefix = "9";
    private const string LocalMobileTrunk = "15";
    private const int NationalSignificantNumberLength = 10;
    private const int LocalWithTrunkPrefixLength = NationalSignificantNumberLength + 2; // + "15".Length

    /// <inheritdoc />
    public string Normalize(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return phoneNumber ?? string.Empty;
        }

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (digits.Length == 0)
        {
            return phoneNumber.Trim();
        }

        if (digits.StartsWith(CountryCode, StringComparison.Ordinal))
        {
            digits = digits[CountryCode.Length..];
        }

        if (digits.StartsWith(MobilePrefix, StringComparison.Ordinal) && digits.Length > NationalSignificantNumberLength)
        {
            digits = digits[MobilePrefix.Length..];
        }

        // Prefijo troncal "0" del formato de marcado local (ej. "011", "0341").
        if (digits.StartsWith("0", StringComparison.Ordinal))
        {
            digits = digits[1..];
        }

        digits = StripLocalMobileTrunkIfPresent(digits);

        return $"{CountryCode}{MobilePrefix}{digits}";
    }

    /// <summary>
    /// Si <paramref name="digits"/> tiene la longitud característica de "área + 15 + abonado"
    /// (12 dígitos: los 10 del número nacional significativo más los 2 del "15"), busca en qué
    /// posición aparece el "15" probando longitudes de código de área de 2 a 4 dígitos, en ese
    /// orden — 2 dígitos (CABA/GBA, código "11") es, con diferencia, el caso más común. Ver la
    /// limitación documentada en el XML doc de la clase. Si no encuentra un calce, o si la
    /// longitud no corresponde a este patrón, devuelve <paramref name="digits"/> sin modificar.
    /// </summary>
    private static string StripLocalMobileTrunkIfPresent(string digits)
    {
        if (digits.Length != LocalWithTrunkPrefixLength)
        {
            return digits;
        }

        for (var areaCodeLength = 2; areaCodeLength <= 4; areaCodeLength++)
        {
            var trunkStart = areaCodeLength;
            var trunkEnd = trunkStart + LocalMobileTrunk.Length;

            if (trunkEnd > digits.Length)
            {
                break;
            }

            if (digits.Substring(trunkStart, LocalMobileTrunk.Length) == LocalMobileTrunk)
            {
                return digits[..areaCodeLength] + digits[trunkEnd..];
            }
        }

        // No se encontró "15" en ninguna posición plausible — se devuelve tal cual en vez de
        // adivinar, para no correr el riesgo de cortar dígitos legítimos del abonado.
        return digits;
    }
}
