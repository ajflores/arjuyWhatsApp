using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ArjuyWhatsApp;

/// <summary>
/// Extensiones de registro de <see cref="IArjuyWhatsAppClient"/> en el contenedor de inyección de dependencias.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="IArjuyWhatsAppClient"/> bindeando las opciones desde una sección de <see cref="IConfiguration"/>
    /// (por defecto, la sección "ArjuyWhatsApp" de <c>appsettings.json</c>).
    /// </summary>
    /// <param name="services">Colección de servicios donde registrar el cliente.</param>
    /// <param name="configuration">Configuración de la aplicación (ej. <c>IConfiguration</c> raíz).</param>
    /// <param name="sectionName">Nombre de la sección de configuración a bindear. Por defecto "ArjuyWhatsApp".</param>
    /// <returns>La misma colección de servicios, para encadenar llamadas.</returns>
    public static IServiceCollection AddArjuyWhatsApp(this IServiceCollection services, IConfiguration configuration, string sectionName = "ArjuyWhatsApp")
    {
        services.Configure<ArjuyWhatsAppOptions>(configuration.GetSection(sectionName));

        RegisterClient(services);

        return services;
    }

    /// <summary>
    /// Registra <see cref="IArjuyWhatsAppClient"/> configurando las opciones directamente en código,
    /// sin depender de <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="services">Colección de servicios donde registrar el cliente.</param>
    /// <param name="configure">Delegado que configura las opciones del cliente.</param>
    /// <returns>La misma colección de servicios, para encadenar llamadas.</returns>
    public static IServiceCollection AddArjuyWhatsApp(this IServiceCollection services, Action<ArjuyWhatsAppOptions> configure)
    {
        services.Configure(configure);

        RegisterClient(services);

        return services;
    }

    // Se registra como Singleton (no Scoped) a propósito: IArjuyWhatsAppClient expone el evento
    // MessageReceived, y un suscriptor típico (por ejemplo, en Program.cs al arrancar la app) se
    // suscribe UNA sola vez con la instancia que le entrega el contenedor. Si el cliente fuera
    // Scoped, cada request HTTP (incluida cada llamada al webhook) resolvería una instancia nueva
    // sin ningún suscriptor — el evento jamás se dispararía para nadie fuera de esa misma request.
    // Con Singleton, la instancia (y sus suscriptores) vive durante toda la vida de la app.
    //
    // El HttpClient nombrado sigue funcionando igual con esta lifetime: IHttpClientFactory ya está
    // diseñado para que HttpClient de vida corta convivan con consumidores Singleton (es el patrón
    // recomendado por Microsoft — el factory maneja el pooling/rotación de HttpMessageHandler por
    // detrás, el Singleton solo llama a CreateClient() en cada request saliente).
    //
    // Para poder resolver IWhatsAppMessageHandler (que sí puede registrarse como Scoped, por
    // ejemplo si necesita un DbContext) desde este Singleton, ProcessWebhookAsync no los inyecta
    // directamente: crea un scope nuevo por mensaje vía IServiceScopeFactory y resuelve los
    // handlers desde ahí. Ver ArjuyWhatsAppClient.DispatchAsync.
    private static void RegisterClient(IServiceCollection services)
    {
        services.AddHttpClient(ArjuyWhatsAppClient.HttpClientName);

        // TryAdd: si el consumidor ya registró su propio IPhoneNumberNormalizer (por ejemplo con
        // una tabla de códigos de área más precisa que la de ArgentinaPhoneNumberNormalizer, o
        // para un país sin normalizador built-in), esa registración gana y esta no hace nada.
        services.TryAddSingleton<IPhoneNumberNormalizer>(serviceProvider =>
        {
            var countryCode = serviceProvider.GetRequiredService<IOptions<ArjuyWhatsAppOptions>>().Value.CountryCode;

            return countryCode?.Trim().ToUpperInvariant() switch
            {
                "AR" => new ArgentinaPhoneNumberNormalizer(),
                _ => new DefaultPhoneNumberNormalizer()
            };
        });

        services.AddSingleton<IArjuyWhatsAppClient, ArjuyWhatsAppClient>();
    }
}
