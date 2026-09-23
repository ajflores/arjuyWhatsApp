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

// Guarda el último message id devuelto por un envío exitoso, para poder reusarlo sin
// tipearlo a mano al probar reacciones o marcar como leído en la misma sesión de consola.
string? ultimoMessageId = null;

var salir = false;

while (!salir)
{
    MostrarMenuPrincipal();

    var opcion = Console.ReadLine();

    switch (opcion)
    {
        case "1":
            ultimoMessageId = await EnviarTextoAsync(whatsAppClient) ?? ultimoMessageId;
            break;
        case "2":
            ultimoMessageId = await EnviarTemplateAsync(whatsAppClient) ?? ultimoMessageId;
            break;
        case "3":
            ultimoMessageId = await SubMenuMediaPorUrlAsync(whatsAppClient) ?? ultimoMessageId;
            break;
        case "4":
            ultimoMessageId = await SubMenuMediaLocalAsync(whatsAppClient) ?? ultimoMessageId;
            break;
        case "5":
            ultimoMessageId = await SubMenuInteractivosAsync(whatsAppClient) ?? ultimoMessageId;
            break;
        case "6":
            ultimoMessageId = await SubMenuOtrosMensajesAsync(whatsAppClient, ultimoMessageId) ?? ultimoMessageId;
            break;
        case "7":
            await ListarPlantillasAsync(whatsAppClient);
            break;
        case "8":
            await SubMenuMarcarComoLeidoAsync(whatsAppClient, ultimoMessageId);
            break;
        case "0":
            salir = true;
            break;
        default:
            Console.WriteLine("Opción inválida.");
            break;
    }

    Console.WriteLine();
}

static void MostrarMenuPrincipal()
{
    Console.WriteLine("=== ArjuyWhatsApp — Sample de envío ===");
    Console.WriteLine("1. Enviar texto libre (SendTextAsync)");
    Console.WriteLine("2. Enviar plantilla aprobada (SendTemplateAsync)");
    Console.WriteLine("3. Media por URL (imagen/documento/audio/video/sticker)");
    Console.WriteLine("4. Subir archivo local y enviar por media_id");
    Console.WriteLine("5. Mensajes interactivos (botones/lista)");
    Console.WriteLine("6. Ubicación / Contacto / Reacción");
    Console.WriteLine("7. Listar plantillas aprobadas (GetMessageTemplatesAsync)");
    Console.WriteLine("8. Marcar como leído / typing indicator");
    Console.WriteLine("0. Salir");
    Console.Write("Elegí una opción: ");
}

// ---------------------------------------------------------------------------------------
// Helpers de entrada/salida por consola
// ---------------------------------------------------------------------------------------

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

static double PedirDouble(string etiqueta, double valorPorDefecto)
{
    Console.Write($"{etiqueta} [{valorPorDefecto.ToString(System.Globalization.CultureInfo.InvariantCulture)}]: ");
    var valor = Console.ReadLine();
    return double.TryParse(valor, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var resultado)
        ? resultado
        : valorPorDefecto;
}

static bool PedirBool(string etiqueta, bool valorPorDefecto)
{
    Console.Write($"{etiqueta} (s/n) [{(valorPorDefecto ? "s" : "n")}]: ");
    var valor = Console.ReadLine();
    return string.IsNullOrWhiteSpace(valor)
        ? valorPorDefecto
        : valor.Trim().ToLowerInvariant() is "s" or "si" or "sí" or "y" or "yes";
}

static string PedirMessageId(string etiqueta, string? sugerido)
{
    var valorPorDefecto = sugerido ?? "wamid.EJEMPLO_REEMPLAZAR";
    Console.Write($"{etiqueta} [{valorPorDefecto}]: ");
    var valor = Console.ReadLine();
    return string.IsNullOrWhiteSpace(valor) ? valorPorDefecto : valor;
}

static string GuessMimeType(string filePath)
{
    return Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}

/// <summary>Muestra el resultado de un envío que devuelve message id, y lo retorna para poder guardarlo como "último message id".</summary>
static string? MostrarResultadoMensaje(MResult<string> result)
{
    if (result.IsSuccess)
    {
        Console.WriteLine($"OK. Message id: {result.Data}");
        return result.Data;
    }

    MostrarError(result);
    return null;
}

/// <summary>Muestra el resultado de una operación que no devuelve message id (mark-as-read, etc.).</summary>
static void MostrarResultado<T>(MResult<T> result)
{
    if (result.IsSuccess)
    {
        Console.WriteLine("OK.");
    }
    else
    {
        MostrarError(result);
    }
}

static void MostrarError(MResult result)
{
    if (result.Error is not null)
    {
        Console.WriteLine($"Error de Meta (HTTP {result.Error.HttpStatusCode}, code={result.Error.Code?.ToString() ?? "?"}, subcode={result.Error.ErrorSubcode?.ToString() ?? "?"}): {result.Message}");
        if (result.Error.IsTransient)
        {
            Console.WriteLine("  (era un error transitorio — la librería ya reintentó automáticamente con backoff antes de fallar del todo)");
        }
    }
    else
    {
        Console.WriteLine($"Error: {result.Message}");
    }
}

// ---------------------------------------------------------------------------------------
// 1-2. Texto / Template
// ---------------------------------------------------------------------------------------

static async Task<string?> EnviarTextoAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var mensaje = PedirConDefault("Mensaje", "Hola desde ArjuyWhatsApp!");

    var result = await client.SendTextAsync(telefono, mensaje);

    return MostrarResultadoMensaje(result);
}

static async Task<string?> EnviarTemplateAsync(IArjuyWhatsAppClient client)
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

    return MostrarResultadoMensaje(result);
}

// ---------------------------------------------------------------------------------------
// 3. Media por URL
// ---------------------------------------------------------------------------------------

static async Task<string?> SubMenuMediaPorUrlAsync(IArjuyWhatsAppClient client)
{
    Console.WriteLine("--- Media por URL ---");
    Console.WriteLine("1. Imagen");
    Console.WriteLine("2. Documento");
    Console.WriteLine("3. Audio");
    Console.WriteLine("4. Video");
    Console.WriteLine("5. Sticker");
    Console.WriteLine("0. Volver");
    Console.Write("Elegí una opción: ");

    return (Console.ReadLine()) switch
    {
        "1" => await EnviarImagenAsync(client),
        "2" => await EnviarDocumentoAsync(client),
        "3" => await EnviarAudioAsync(client),
        "4" => await EnviarVideoAsync(client),
        "5" => await EnviarStickerAsync(client),
        _ => null
    };
}

static async Task<string?> EnviarImagenAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var imageUrl = PedirConDefault("URL pública de la imagen", "https://picsum.photos/400");
    var caption = PedirConDefault("Caption (opcional)", string.Empty);

    var result = await client.SendImageAsync(telefono, imageUrl, string.IsNullOrWhiteSpace(caption) ? null : caption);

    return MostrarResultadoMensaje(result);
}

static async Task<string?> EnviarDocumentoAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var documentUrl = PedirConDefault("URL pública del documento", "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf");
    var fileName = PedirConDefault("Nombre de archivo", "documento.pdf");
    var caption = PedirConDefault("Caption (opcional)", string.Empty);

    var result = await client.SendDocumentAsync(telefono, documentUrl, fileName, string.IsNullOrWhiteSpace(caption) ? null : caption);

    return MostrarResultadoMensaje(result);
}

static async Task<string?> EnviarAudioAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var audioUrl = PedirConDefault("URL pública del audio (OGG/Opus para nota de voz)", "https://www.w3schools.com/html/horse.ogg");
    var esNotaDeVoz = PedirBool("¿Mostrar como nota de voz? (voice)", false);

    var result = await client.SendAudioAsync(telefono, audioUrl, esNotaDeVoz);

    return MostrarResultadoMensaje(result);
}

static async Task<string?> EnviarVideoAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var videoUrl = PedirConDefault("URL pública del video (H.264/AAC)", "https://www.w3schools.com/html/mov_bbb.mp4");
    var caption = PedirConDefault("Caption (opcional)", string.Empty);

    var result = await client.SendVideoAsync(telefono, videoUrl, string.IsNullOrWhiteSpace(caption) ? null : caption);

    return MostrarResultadoMensaje(result);
}

static async Task<string?> EnviarStickerAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var stickerUrl = PedirConDefault("URL pública del sticker (WebP)", "https://www.gstatic.com/webp/gallery/1.webp");

    var result = await client.SendStickerAsync(telefono, stickerUrl);

    return MostrarResultadoMensaje(result);
}

// ---------------------------------------------------------------------------------------
// 4. Subir archivo local y enviar por media_id
// ---------------------------------------------------------------------------------------

static async Task<string?> SubMenuMediaLocalAsync(IArjuyWhatsAppClient client)
{
    Console.WriteLine("--- Subir archivo local y enviar por media_id ---");
    Console.WriteLine("1. Imagen");
    Console.WriteLine("2. Documento");
    Console.WriteLine("0. Volver");
    Console.Write("Elegí una opción: ");

    return (Console.ReadLine()) switch
    {
        "1" => await SubirYEnviarImagenAsync(client),
        "2" => await SubirYEnviarDocumentoAsync(client),
        _ => null
    };
}

static async Task<(byte[] Bytes, string FileName, string Path)?> LeerArchivoLocalAsync()
{
    Console.Write("Ruta del archivo local: ");
    var path = Console.ReadLine() ?? string.Empty;

    if (!File.Exists(path))
    {
        Console.WriteLine("No se encontró el archivo en esa ruta.");
        return null;
    }

    var bytes = await File.ReadAllBytesAsync(path);
    return (bytes, Path.GetFileName(path), path);
}

static async Task<string?> SubirYEnviarImagenAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();

    var archivo = await LeerArchivoLocalAsync();
    if (archivo is null)
    {
        return null;
    }

    var (bytes, fileName, path) = archivo.Value;
    var mimeType = PedirConDefault("Tipo MIME", GuessMimeType(path));

    var uploadResult = await client.UploadMediaAsync(bytes, fileName, mimeType);
    if (!uploadResult.IsSuccess)
    {
        MostrarError(uploadResult);
        return null;
    }

    Console.WriteLine($"Upload OK. media_id: {uploadResult.Data}");

    var caption = PedirConDefault("Caption (opcional)", string.Empty);
    var result = await client.SendImageByMediaIdAsync(telefono, uploadResult.Data!, string.IsNullOrWhiteSpace(caption) ? null : caption);

    return MostrarResultadoMensaje(result);
}

static async Task<string?> SubirYEnviarDocumentoAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();

    var archivo = await LeerArchivoLocalAsync();
    if (archivo is null)
    {
        return null;
    }

    var (bytes, fileName, path) = archivo.Value;
    var mimeType = PedirConDefault("Tipo MIME", GuessMimeType(path));

    var uploadResult = await client.UploadMediaAsync(bytes, fileName, mimeType);
    if (!uploadResult.IsSuccess)
    {
        MostrarError(uploadResult);
        return null;
    }

    Console.WriteLine($"Upload OK. media_id: {uploadResult.Data}");

    var caption = PedirConDefault("Caption (opcional)", string.Empty);
    var result = await client.SendDocumentByMediaIdAsync(telefono, uploadResult.Data!, fileName, string.IsNullOrWhiteSpace(caption) ? null : caption);

    return MostrarResultadoMensaje(result);
}

// ---------------------------------------------------------------------------------------
// 5. Interactivos
// ---------------------------------------------------------------------------------------

static async Task<string?> SubMenuInteractivosAsync(IArjuyWhatsAppClient client)
{
    Console.WriteLine("--- Mensajes interactivos ---");
    Console.WriteLine("1. Botones (Confirmar / Cancelar)");
    Console.WriteLine("2. Lista (2 secciones de ejemplo)");
    Console.WriteLine("0. Volver");
    Console.Write("Elegí una opción: ");

    return (Console.ReadLine()) switch
    {
        "1" => await EnviarBotonesAsync(client),
        "2" => await EnviarListaAsync(client),
        _ => null
    };
}

static async Task<string?> EnviarBotonesAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var bodyText = PedirConDefault("Texto del mensaje", "¿Confirmás tu reserva?");

    var botones = new (string Id, string Title)[]
    {
        ("confirmar", "Confirmar"),
        ("cancelar", "Cancelar")
    };

    var result = await client.SendInteractiveButtonsAsync(telefono, bodyText, botones);

    return MostrarResultadoMensaje(result);
}

static async Task<string?> EnviarListaAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var bodyText = PedirConDefault("Texto del mensaje", "Elegí una opción:");

    var secciones = new (string SectionTitle, IEnumerable<(string Id, string Title, string? Description)> Rows)[]
    {
        ("Servicios", new (string Id, string Title, string? Description)[]
        {
            ("consulta", "Consulta general", "Hacé una pregunta"),
            ("soporte", "Soporte técnico", "Reportá un problema")
        }),
        ("Ventas", new (string Id, string Title, string? Description)[]
        {
            ("precios", "Ver precios", null),
            ("asesor", "Hablar con un asesor", null)
        })
    };

    var result = await client.SendInteractiveListAsync(telefono, bodyText, "Ver opciones", secciones);

    return MostrarResultadoMensaje(result);
}

// ---------------------------------------------------------------------------------------
// 6. Ubicación / Contacto / Reacción
// ---------------------------------------------------------------------------------------

static async Task<string?> SubMenuOtrosMensajesAsync(IArjuyWhatsAppClient client, string? ultimoMessageId)
{
    Console.WriteLine("--- Ubicación / Contacto / Reacción ---");
    Console.WriteLine("1. Ubicación");
    Console.WriteLine("2. Contacto");
    Console.WriteLine("3. Reacción a un mensaje");
    Console.WriteLine("0. Volver");
    Console.Write("Elegí una opción: ");

    return (Console.ReadLine()) switch
    {
        "1" => await EnviarUbicacionAsync(client),
        "2" => await EnviarContactoAsync(client),
        "3" => await EnviarReaccionAsync(client, ultimoMessageId),
        _ => null
    };
}

static async Task<string?> EnviarUbicacionAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    // Coordenadas de ejemplo: plaza principal de San Salvador de Jujuy.
    var latitud = PedirDouble("Latitud", -24.1858);
    var longitud = PedirDouble("Longitud", -65.2995);
    var nombre = PedirConDefault("Nombre del lugar (opcional)", "San Salvador de Jujuy");
    var direccion = PedirConDefault("Dirección (opcional)", string.Empty);

    var result = await client.SendLocationAsync(
        telefono,
        latitud,
        longitud,
        string.IsNullOrWhiteSpace(nombre) ? null : nombre,
        string.IsNullOrWhiteSpace(direccion) ? null : direccion);

    return MostrarResultadoMensaje(result);
}

static async Task<string?> EnviarContactoAsync(IArjuyWhatsAppClient client)
{
    var telefono = PedirTelefono();
    var nombreContacto = PedirConDefault("Nombre completo del contacto a enviar", "Comunidad ArjuyDev");
    var telefonoContacto = PedirConDefault("Teléfono del contacto", "5493880000000");

    var contacto = new WhatsAppContact
    {
        Name = new WhatsAppContactName { FormattedName = nombreContacto },
        Phones = [new WhatsAppContactPhone { Phone = telefonoContacto, Type = "WORK" }]
    };

    var result = await client.SendContactsAsync(telefono, [contacto]);

    return MostrarResultadoMensaje(result);
}

static async Task<string?> EnviarReaccionAsync(IArjuyWhatsAppClient client, string? ultimoMessageId)
{
    var telefono = PedirTelefono();
    var messageId = PedirMessageId("Id del mensaje a reaccionar (wamid.)", ultimoMessageId);
    Console.Write("Emoji a enviar (dejá vacío para REMOVER una reacción existente, ej. 👍): ");
    var emoji = Console.ReadLine() ?? string.Empty;

    var result = await client.SendReactionAsync(telefono, messageId, emoji);

    return MostrarResultadoMensaje(result);
}

// ---------------------------------------------------------------------------------------
// 7. Listar plantillas
// ---------------------------------------------------------------------------------------

static async Task ListarPlantillasAsync(IArjuyWhatsAppClient client)
{
    var result = await client.GetMessageTemplatesAsync();

    if (!result.IsSuccess)
    {
        MostrarError(result);
        return;
    }

    if (result.Data is null || result.Data.Count == 0)
    {
        Console.WriteLine("No hay plantillas configuradas en esta cuenta.");
        return;
    }

    Console.WriteLine($"{"Nombre",-30} {"Idioma",-8} {"Estado",-12} {"Categoría",-12}");
    Console.WriteLine(new string('-', 64));

    foreach (var template in result.Data)
    {
        Console.WriteLine($"{template.Name,-30} {template.Language,-8} {template.Status,-12} {template.Category,-12}");

        if (template.Status == WhatsAppMessageTemplateStatus.Rejected && !string.IsNullOrWhiteSpace(template.RejectedReason))
        {
            Console.WriteLine($"    Motivo de rechazo: {template.RejectedReason}");
        }
    }

    Console.WriteLine($"Total: {result.Data.Count} plantilla(s).");
}

// ---------------------------------------------------------------------------------------
// 8. Marcar como leído / typing indicator
// ---------------------------------------------------------------------------------------

static async Task SubMenuMarcarComoLeidoAsync(IArjuyWhatsAppClient client, string? ultimoMessageId)
{
    Console.WriteLine("--- Marcar como leído ---");
    Console.WriteLine("1. Marcar como leído (MarkAsReadAsync)");
    Console.WriteLine("2. Marcar como leído + typing indicator (MarkAsReadWithTypingIndicatorAsync)");
    Console.WriteLine("0. Volver");
    Console.Write("Elegí una opción: ");

    var opcion = Console.ReadLine();
    if (opcion is not ("1" or "2"))
    {
        return;
    }

    var messageId = PedirMessageId("Id del mensaje entrante a marcar como leído (wamid.)", ultimoMessageId);

    var result = opcion == "2"
        ? await client.MarkAsReadWithTypingIndicatorAsync(messageId)
        : await client.MarkAsReadAsync(messageId);

    MostrarResultado(result);
}
