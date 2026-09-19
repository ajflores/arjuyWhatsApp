using ArjuyWhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// -----------------------------------------------------------------------
// ArjuyWhatsApp.Sample: menú interactivo simple para probar cada método de ENVÍO de
// IArjuyWhatsAppClient a mano, sin necesitar Postman ni levantar un servidor HTTP.
//
// Para probar RECEPCIÓN (webhook) hace falta un servidor HTTP real expuesto a internet —
// ver el proyecto ArjuyWhatsApp.Sample.Api y la sección "Guía de prueba end-to-end" en
// MANUAL.md.
// -----------------------------------------------------------------------

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();

services.AddArjuyWhatsApp(configuration);

using var serviceProvider = services.BuildServiceProvider();

var whatsAppClient = serviceProvider.GetRequiredService<IArjuyWhatsAppClient>();

var salir = false;

while (!salir)
{
    MostrarMenu();

    var opcion = Console.ReadLine();

    switch (opcion)
    {
        case "1":
            await EnviarTextoAsync(whatsAppClient);
            break;
        case "2":
            await EnviarTemplateAsync(whatsAppClient);
            break;
        case "3":
            await EnviarImagenAsync(whatsAppClient);
            break;
        case "4":
            await EnviarDocumentoAsync(whatsAppClient);
            break;
        case "5":
            salir = true;
            break;
        default:
            Console.WriteLine("Opción inválida. Elegí un número del 1 al 5.");
            break;
    }

    Console.WriteLine();
}

static void MostrarMenu()
{
    Console.WriteLine("=== ArjuyWhatsApp — Sample de envío ===");
    Console.WriteLine("1. Enviar texto libre (SendTextAsync)");
    Console.WriteLine("2. Enviar plantilla aprobada (SendTemplateAsync)");
    Console.WriteLine("3. Enviar imagen por URL (SendImageAsync)");
    Console.WriteLine("4. Enviar documento por URL (SendDocumentAsync)");
    Console.WriteLine("5. Salir");
    Console.Write("Elegí una opción: ");
}

static string PedirTelefono()
{
    Console.Write("Número de teléfono destino (formato internacional, ej. 5491100000000): ");
    return Console.ReadLine() ?? string.Empty;
}

static string PedirConDefault(string etiqueta, string valorPorDefecto)
{
    Console.Write($"{etiqueta} [{valorPorDefecto}]: ");
    var valor = Console.ReadLine();
    return string.IsNullOrWhiteSpace(valor) ? valorPorDefecto : valor;
}

static void MostrarResultado<T>(MResult<T> result)
{
    if (result.IsSuccess)
    {
        Console.WriteLine($"OK. Message id: {result.Data}");
    }
    else
    {
        Console.WriteLine($"Error: {result.Message}");
    }
}

static async Task EnviarTextoAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var mensaje = PedirConDefault("Mensaje", "Hola desde ArjuyWhatsApp!");

    var result = await client.SendTextAsync(telefono, mensaje);

    MostrarResultado(result);
}

static async Task EnviarTemplateAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var templateName = PedirConDefault("Nombre de la plantilla", "hello_world");
    var languageCode = PedirConDefault("Código de idioma", "en_US");
    Console.Write("Parámetros del cuerpo separados por coma (dejar vacío si no tiene): ");
    var parametrosRaw = Console.ReadLine();

    var parametros = string.IsNullOrWhiteSpace(parametrosRaw)
        ? Array.Empty<string>()
        : parametrosRaw.Split(',', StringSplitOptions.TrimEntries);

    var result = await client.SendTemplateAsync(telefono, templateName, languageCode, parametros);

    MostrarResultado(result);
}

static async Task EnviarImagenAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var imageUrl = PedirConDefault("URL pública de la imagen", "https://picsum.photos/400");
    var caption = PedirConDefault("Caption (opcional)", string.Empty);

    var result = await client.SendImageAsync(telefono, imageUrl, string.IsNullOrWhiteSpace(caption) ? null : caption);

    MostrarResultado(result);
}

static async Task EnviarDocumentoAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var documentUrl = PedirConDefault("URL pública del documento", "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf");
    var fileName = PedirConDefault("Nombre de archivo", "documento.pdf");
    var caption = PedirConDefault("Caption (opcional)", string.Empty);

    var result = await client.SendDocumentAsync(telefono, documentUrl, fileName, string.IsNullOrWhiteSpace(caption) ? null : caption);

    MostrarResultado(result);
}
