namespace ArjuyWhatsApp;

/// <summary>
/// Implementación por defecto de <see cref="IPhoneNumberNormalizer"/>: no aplica ninguna regla
/// específica de país, solo recorta espacios en blanco. Se auto-registra cuando
/// <see cref="ArjuyWhatsAppOptions.CountryCode"/> no está configurado o no coincide con ningún
/// normalizador built-in de la librería (ver <c>ServiceCollectionExtensions</c>).
/// </summary>
public class DefaultPhoneNumberNormalizer : IPhoneNumberNormalizer
{
    /// <inheritdoc />
    public string Normalize(string phoneNumber)
    {
        return phoneNumber.Trim();
    }
}
