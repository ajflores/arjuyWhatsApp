namespace ArjuyWhatsApp;

/// <summary>
/// Estrategia de normalización del número de teléfono de destino antes de enviarlo en el campo
/// <c>"to"</c> del payload a la Graph API de Meta. Se resuelve por inyección de dependencias —
/// para usar una implementación propia, registrala en el contenedor ANTES de llamar a
/// <c>AddArjuyWhatsApp</c> (la librería solo registra una implementación built-in si no hay
/// ninguna ya registrada, ver <c>ServiceCollectionExtensions</c>), o simplemente no configurés
/// <see cref="ArjuyWhatsAppOptions.CountryCode"/> y registrá la tuya a mano.
/// </summary>
public interface IPhoneNumberNormalizer
{
    /// <summary>
    /// Normaliza un número de teléfono al formato que espera Meta en el campo <c>"to"</c>
    /// (formato internacional, con código de país, sin <c>"+"</c>).
    /// </summary>
    /// <param name="phoneNumber">Número de teléfono tal como lo recibe la aplicación consumidora.</param>
    /// <returns>Número normalizado. Debe ser idempotente: aplicarla sobre su propio resultado tiene que devolver el mismo valor.</returns>
    string Normalize(string phoneNumber);
}
