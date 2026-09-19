# Manual de uso — ArjuyWhatsApp

Guía extendida de uso de la librería. Si buscás un quickstart corto, ver `README.md`.
Este manual cubre: instalación y registro, envío de mensajes, el flujo completo de
**recepción de mensajes vía webhook** (verify challenge, validación de firma HMAC y parseo del
payload de Meta ya resueltos por la librería — ver sección 3 — incluyendo cómo probarlo en local
con ngrok), troubleshooting, y una **guía de prueba end-to-end** (sección 5) que te lleva paso a
paso a probar envío y recepción reales contra el número de prueba de Meta.

> Los nombres de tipos, propiedades y métodos citados en este manual fueron verificados contra el
> código actual del proyecto (`ArjuyWhatsAppOptions.cs`, `ServiceCollectionExtensions.cs`,
> `IArjuyWhatsAppClient.cs`, `ArjuyWhatsAppClient.cs`, `WhatsAppMessageReceived.cs`,
> `IWhatsAppMessageHandler.cs`, `MResult.cs`) al momento de escribir este documento. El tipo de
> resultado se llama **`MResult<T>`** (y `MResult` sin genérico para operaciones sin dato de
> retorno) — no `ArjuyResult`.

---

## 1. Instalación y registro

### 1.1. Instalación del paquete

```bash
dotnet add package ArjuyWhatsApp
```

### 1.2. Opciones de configuración (`ArjuyWhatsAppOptions`)

Todo el comportamiento del cliente se configura a través de `ArjuyWhatsAppOptions`:

| Propiedad | Tipo | Qué es | Dónde se consigue |
|---|---|---|---|
| `AccessToken` | `string` | El "pase" con el que la librería se autentica contra la Graph API de Meta en cada llamado (enviar un mensaje, descargar media, etc.). Sin uno válido, todo falla con 401/403. | En tu app de Meta, sección **WhatsApp > API Setup**, hay un botón para generar un token. **Ojo con esto**: ese botón te da por defecto un token **temporal de 24 hs**, pensado solo para probar en el momento — si lo usás en un proyecto real se va a cortar solo al otro día y vas a tener que estar regenerándolo a mano. Para producción, generá en cambio un token **permanente** a través de un **System User** en Business Settings de Meta Business Suite (Business Settings > Users > System Users), asignale el permiso sobre tu app/WABA, y generá el token desde ahí — ese no expira mientras el System User siga activo. |
| `PhoneNumberId` | `string` | El ID interno que usa Meta para identificar el número de WhatsApp desde el que se manda/recibe (**no** es el número de teléfono en sí, es un ID numérico largo). | Misma pantalla, **WhatsApp > API Setup** — ahí Meta lo muestra al lado del número de prueba (o tu número productivo, si ya migraste uno propio). |
| `BusinessAccountId` | `string` | El ID de tu WhatsApp Business Account (WABA) — la "cuenta madre" que agrupa uno o más números de teléfono y las plantillas aprobadas. Se usa para operaciones a nivel cuenta, como listar plantillas. | Visible en la configuración de la app (sección WhatsApp, junto a los demás IDs) o en Business Settings de Meta Business Suite, dentro de la sección de cuentas de WhatsApp. |
| `AppSecret` | `string` | Un secreto propio de la App de Meta (no de WhatsApp específicamente) que la librería usa como clave para validar que un webhook entrante realmente vino de Meta y no fue falsificado (firma HMAC-SHA256, ver sección 3.4). | En la configuración de la App de Meta, sección **Configuración básica** ("App Secret") — está oculto por defecto, hay que clickear "Mostrar" (a veces te pide reautenticarte con tu contraseña de Facebook/Meta para mostrarlo). |
| `VerifyToken` | `string` | Un string secreto que sirve para que Meta confirme, la primera vez que configurás el webhook, que la URL le pertenece a alguien autorizado (el "handshake" de verificación, sección 3.3). | **Este valor NO te lo da Meta** — es al revés: **lo inventás vos** (cualquier string que quieras, tipo password) y lo tenés que poner en **dos lugares que tienen que coincidir exactamente**: acá, en tu configuración (`ArjuyWhatsAppOptions.VerifyToken`), y en el panel de Meta al cargar el **Callback URL** del webhook (campo "Verify Token", ver sección 3.2). Si buscás este valor "en algún lado" del panel de Meta y no lo encontrás, es porque no existe hasta que vos lo escribís ahí — es la confusión más común con esta propiedad. |
| `ApiVersion` | `string` | La versión de la Graph API de Meta contra la que pega la librería (por defecto `"v21.0"`, ya seteado en el código). | No se "consigue" en ningún panel — es un dato que vos elegís/actualizás. Meta depreca versiones viejas de su API periódicamente (suele avisar con varios meses de anticipación), así que cada tanto conviene revisar la [documentación de versionado de Graph API](https://developers.facebook.com/docs/graph-api/guides/versioning) y subir este valor antes de que la versión que estás usando deje de funcionar. |

`AppSecret` y `VerifyToken` son usados internamente por `IArjuyWhatsAppClient.VerifyWebhookChallenge`
y `ProcessWebhookAsync` — ver sección 3 para el flujo completo de recepción de mensajes, y sección
3.2 en particular para dónde se cargan `Callback URL` y `Verify Token` del lado de Meta.

> **No commitees estos valores al repositorio.** `AccessToken`, `AppSecret` y `VerifyToken` son
> credenciales reales — si las hardcodeás en un `appsettings.json` versionado, quedan expuestas
> para siempre en el historial de git (aunque las borres después). Usá en cambio:
>
> - `appsettings.Development.json` con esos valores reales, agregado a `.gitignore` (dejá en el
>   `appsettings.json` versionado placeholders tipo `"TU_ACCESS_TOKEN"`, como en el ejemplo de 1.3).
> - `dotnet user-secrets` para desarrollo local (`dotnet user-secrets set "ArjuyWhatsApp:AccessToken" "..."`),
>   que guarda los valores fuera del repo, en tu perfil de usuario.
> - Un secret manager real (Azure Key Vault, AWS Secrets Manager, variables de entorno del
>   hosting, etc.) en producción — nunca el `appsettings.json` que se sube al servidor vía control
>   de versiones.

### 1.3. Modo 1 — `AddArjuyWhatsApp(IConfiguration)`

Registra el cliente bindeando una sección de `IConfiguration` (por defecto, la sección
`"ArjuyWhatsApp"`).

**`Program.cs`:**

```csharp
using ArjuyWhatsApp;

var builder = Host.CreateApplicationBuilder(args);

// Lee la sección "ArjuyWhatsApp" de appsettings.json (nombre de sección configurable
// con el parámetro opcional sectionName).
builder.Services.AddArjuyWhatsApp(builder.Configuration);
```

**`appsettings.json`:**

```json
{
  "ArjuyWhatsApp": {
    "AccessToken": "TU_ACCESS_TOKEN",
    "PhoneNumberId": "TU_PHONE_NUMBER_ID",
    "BusinessAccountId": "TU_BUSINESS_ACCOUNT_ID",
    "AppSecret": "TU_APP_SECRET",
    "VerifyToken": "TU_VERIFY_TOKEN",
    "ApiVersion": "v21.0"
  }
}
```

Si querés usar otro nombre de sección, pasalo como segundo parámetro:

```csharp
builder.Services.AddArjuyWhatsApp(builder.Configuration, sectionName: "WhatsApp");
```

### 1.4. Modo 2 — `AddArjuyWhatsApp(Action<ArjuyWhatsAppOptions>)`

Configura las opciones directamente en código, sin depender de `IConfiguration` (útil para
tests, scripts, o cuando los valores vienen de otro lado, como un secret manager):

```csharp
using ArjuyWhatsApp;

services.AddArjuyWhatsApp(options =>
{
    options.AccessToken = "TU_ACCESS_TOKEN";
    options.PhoneNumberId = "TU_PHONE_NUMBER_ID";
    options.BusinessAccountId = "TU_BUSINESS_ACCOUNT_ID";
    options.AppSecret = "TU_APP_SECRET";
    options.VerifyToken = "TU_VERIFY_TOKEN";
    options.ApiVersion = "v21.0";
});
```

> Usá una sola de las dos opciones por contenedor de DI: ambas registran la misma interfaz
> (`IArjuyWhatsAppClient`) contra el mismo `services.AddScoped<IArjuyWhatsAppClient, ArjuyWhatsAppClient>()`
> interno, y la última en ejecutarse gana la configuración de `ArjuyWhatsAppOptions`.

---

## 2. Envío de mensajes

Todos los métodos de `IArjuyWhatsAppClient` son asincrónicos y devuelven `MResult<T>`: un tipo de
resultado propio (sin dependencias de terceros) con `IsSuccess`, `Data` y `Message`. Ninguno lanza
excepciones para errores esperables de red/HTTP — esas se capturan internamente y se traducen a
`MResult.Fail(...)`.

Obtené la instancia del cliente vía DI:

```csharp
var whatsAppClient = serviceProvider.GetRequiredService<IArjuyWhatsAppClient>();
```

### 2.1. Texto libre — `SendTextAsync`

```csharp
Task<MResult<string>> SendTextAsync(string phoneNumber, string message)
```

```csharp
var result = await whatsAppClient.SendTextAsync("5491100000000", "Hola desde ArjuyWhatsApp!");

if (result.IsSuccess)
{
    Console.WriteLine($"Mensaje enviado. Message id: {result.Data}");
}
else
{
    Console.WriteLine($"Error al enviar el mensaje: {result.Message}");
}
```

### 2.2. Plantilla aprobada — `SendTemplateAsync`

```csharp
Task<MResult<string>> SendTemplateAsync(
    string phoneNumber,
    string templateName,
    string languageCode,
    IEnumerable<string> parameters)
```

```csharp
var result = await whatsAppClient.SendTemplateAsync(
    phoneNumber: "5491100000000",
    templateName: "confirmacion_turno",
    languageCode: "es_AR",
    parameters: new[] { "Juan", "17/09/2026 10:00hs" });

if (!result.IsSuccess)
{
    Console.WriteLine($"Error al enviar la plantilla: {result.Message}");
}
```

`parameters` son los parámetros posicionales del cuerpo de la plantilla, en el mismo orden en que
aparecen los placeholders (`{{1}}`, `{{2}}`, ...) en la plantilla aprobada por Meta.

### 2.3. Imagen por URL — `SendImageAsync`

```csharp
Task<MResult<string>> SendImageAsync(string phoneNumber, string imageUrl, string? caption = null)
```

```csharp
var result = await whatsAppClient.SendImageAsync(
    phoneNumber: "5491100000000",
    imageUrl: "https://miapp.com/imagenes/promo.jpg",
    caption: "Promo de la semana");
```

La imagen se envía por URL pública — Meta la descarga desde ahí, no se sube el binario desde tu
proceso.

### 2.4. Documento por URL — `SendDocumentAsync`

```csharp
Task<MResult<string>> SendDocumentAsync(
    string phoneNumber,
    string documentUrl,
    string fileName,
    string? caption = null)
```

```csharp
var result = await whatsAppClient.SendDocumentAsync(
    phoneNumber: "5491100000000",
    documentUrl: "https://miapp.com/docs/factura-1024.pdf",
    fileName: "factura-1024.pdf",
    caption: "Tu factura del mes");
```

### 2.5. Descarga de media recibida — `DownloadMediaAsync`

```csharp
Task<MResult<byte[]>> DownloadMediaAsync(string mediaId)
```

Se usa típicamente con el `media id` que llega en el body de un webhook entrante (ver sección 3),
cuando un usuario te manda una imagen, audio o documento por WhatsApp:

```csharp
var result = await whatsAppClient.DownloadMediaAsync(mediaId);

if (result.IsSuccess)
{
    await File.WriteAllBytesAsync("media-descargada.bin", result.Data!);
}
else
{
    Console.WriteLine($"Error al descargar media: {result.Message}");
}
```

### 2.6. Enviar un archivo local (sin URL pública) — `UploadMediaAsync` + `SendImageByMediaIdAsync` / `SendDocumentByMediaIdAsync`

`SendImageAsync` y `SendDocumentAsync` (secciones 2.3 y 2.4) requieren una URL *pública* — Meta
descarga el archivo desde ahí. Eso funciona bien para material ya alojado en un CDN o storage
público, pero muchos casos reales generan un archivo **privado** en el momento (por ejemplo, un
PDF de una reserva armado al vuelo) que no querés ni podés exponer en una URL accesible desde
internet.

Para esos casos, Meta ofrece un endpoint de upload separado (`POST /{phone-number-id}/media`)
que sube el archivo directamente y devuelve un `media_id`; ese id se usa después en el mensaje en
vez de un `link`. La librería expone esto como dos pasos explícitos:

```csharp
Task<MResult<string>> UploadMediaAsync(byte[] fileContent, string fileName, string mimeType)

Task<MResult<string>> SendImageByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null)
Task<MResult<string>> SendDocumentByMediaIdAsync(string phoneNumber, string mediaId, string? fileName = null, string? caption = null)
```

**¿Cuándo usar URL vs. cuándo subir el archivo?**

| Situación | Método recomendado |
|---|---|
| El archivo ya vive en un storage/CDN público (imágenes de catálogo, banners) | `SendImageAsync` / `SendDocumentAsync` (URL) |
| El archivo es privado o se genera en el momento (reserva, factura, comprobante) y no querés exponerlo en una URL pública | `UploadMediaAsync` + `SendImageByMediaIdAsync` / `SendDocumentByMediaIdAsync` |

> Nota de diseño: se agregaron como métodos nuevos (`...ByMediaIdAsync`) en vez de sobrecargar
> `SendImageAsync`/`SendDocumentAsync` con un parámetro ambiguo — tanto una URL como un `media_id`
> son `string`, y un overload por tipo de parámetro haría fácil pasar el valor equivocado sin que
> el compilador lo detecte. El nombre explícito elimina esa ambigüedad.

Ejemplo encadenado — generar un PDF de reserva en memoria, subirlo, y enviarlo por su `media_id`:

```csharp
byte[] pdfBytes = GenerarPdfDeReserva(reservaId); // tu propia lógica, nunca toca una URL pública

var uploadResult = await whatsAppClient.UploadMediaAsync(
    fileContent: pdfBytes,
    fileName: "reserva-1024.pdf",
    mimeType: "application/pdf");

if (!uploadResult.IsSuccess)
{
    Console.WriteLine($"Error al subir el archivo: {uploadResult.Message}");
    return;
}

var sendResult = await whatsAppClient.SendDocumentByMediaIdAsync(
    phoneNumber: "5491100000000",
    mediaId: uploadResult.Data!,
    fileName: "reserva-1024.pdf",
    caption: "Tu reserva confirmada");

if (!sendResult.IsSuccess)
{
    Console.WriteLine($"Error al enviar el documento: {sendResult.Message}");
}
```

El mismo patrón aplica a imágenes con `SendImageByMediaIdAsync`. El `media_id` devuelto por
`UploadMediaAsync` queda asociado a tu número de WhatsApp Business y puede reutilizarse en varios
envíos mientras no expire (Meta no documenta un TTL fijo — si el envío falla por media id
inválido/expirado, subí el archivo de nuevo).

### 2.7. Mensajes interactivos — botones y listas

Meta soporta un tipo de mensaje `interactive` con dos variantes, pensadas para que el usuario
elija una opción tocando la pantalla en vez de escribir texto libre (mucho más confiable para
lógica de negocio — no hay que interpretar lenguaje natural, llega un `id` exacto):

| Variante | Cuándo usarla | Límite de Meta |
|---|---|---|
| **Botones** (`SendInteractiveButtonsAsync`) | 2 o 3 opciones simples, sin necesidad de texto adicional por opción (ej. "Confirmar" / "Cancelar", "Sí" / "No" / "Hablar con un asesor") | Máximo **3 botones**; título de cada botón hasta **20 caracteres**, único entre los botones del mensaje; `body` hasta 1024 caracteres |
| **Lista** (`SendInteractiveListAsync`) | Más de 3 opciones, o cuando cada opción se beneficia de una descripción corta (ej. un menú de excursiones, un catálogo de servicios) | Máximo **10 secciones**, máximo **10 filas en total** sumando todas las secciones; título de fila hasta 24 caracteres, descripción opcional hasta 72 caracteres; texto del botón que abre el menú hasta 20 caracteres; `body` hasta 4096 caracteres |

Límites confirmados contra la documentación oficial de Meta (WhatsApp Cloud API — "Interactive
Reply Buttons Messages" e "Interactive List Messages",
`developers.facebook.com/docs/whatsapp/cloud-api/messages/`) al 2026-09-17. La librería valida
estos límites **antes** de llamar a la API — si te pasás, `MResult<string>.Fail(...)` te lo dice
con un mensaje claro en vez de esperar a que Meta devuelva un 400 críptico.

#### Botones — `SendInteractiveButtonsAsync`

```csharp
Task<MResult<string>> SendInteractiveButtonsAsync(
    string phoneNumber,
    string bodyText,
    IEnumerable<(string Id, string Title)> buttons)
```

```csharp
var result = await whatsAppClient.SendInteractiveButtonsAsync(
    phoneNumber: "5491100000000",
    bodyText: "¿Confirmás tu reserva para mañana a las 10hs?",
    buttons: new[]
    {
        ("confirmar", "Confirmar"),
        ("cancelar", "Cancelar")
    });

if (!result.IsSuccess)
{
    Console.WriteLine($"Error al enviar los botones: {result.Message}");
}
```

`Id` es el valor que te va a volver en el webhook cuando el usuario toque el botón (ver más
abajo) — usalo como clave de tu lógica de negocio, no el `Title` (que es solo lo que ve el
usuario y puede cambiar de idioma/redacción sin romper tu código).

#### Lista — `SendInteractiveListAsync`

```csharp
Task<MResult<string>> SendInteractiveListAsync(
    string phoneNumber,
    string bodyText,
    string buttonText,
    IEnumerable<(string SectionTitle, IEnumerable<(string Id, string Title, string? Description)> Rows)> sections)
```

```csharp
var result = await whatsAppClient.SendInteractiveListAsync(
    phoneNumber: "5491100000000",
    bodyText: "Elegí la excursión que más te interese:",
    buttonText: "Ver opciones",
    sections: new[]
    {
        ("Excursiones de día completo", (IEnumerable<(string Id, string Title, string? Description)>)new[]
        {
            ("full-glaciar", "Full day glaciar", (string?)"Salida 7am, incluye almuerzo"),
            ("full-lago", "Full day lago", (string?)"Salida 8am, navegación incluida")
        }),
        ("Excursiones medio día", (IEnumerable<(string Id, string Title, string? Description)>)new[]
        {
            ("half-city", "City tour", (string?)null)
        })
    });

if (!result.IsSuccess)
{
    Console.WriteLine($"Error al enviar la lista: {result.Message}");
}
```

#### Leer la respuesta del usuario — webhook

Cuando el destinatario toca un botón o elige una fila, Meta lo entrega por el mismo mecanismo de
la sección 3 (evento `MessageReceived` / `IWhatsAppMessageHandler`), como un mensaje con
`Type == WhatsAppMessageType.Interactive`:

```csharp
whatsAppClient.MessageReceived += (_, args) =>
{
    var message = args.Message;

    if (message.Type == WhatsAppMessageType.Interactive)
    {
        // InteractiveReplyId es el mismo Id que le pasaste a SendInteractiveButtonsAsync /
        // SendInteractiveListAsync — usalo para tu lógica de negocio (switch, diccionario, etc.).
        switch (message.InteractiveReplyId)
        {
            case "confirmar":
                // ...
                break;
            case "cancelar":
                // ...
                break;
        }

        // InteractiveReplyTitle es solo para mostrar/loguear (lo que vio el usuario).
        Console.WriteLine($"{message.From} eligió: {message.InteractiveReplyTitle}");
    }
};
```

`InteractiveReplyId` y `InteractiveReplyTitle` quedan en `null` para cualquier otro `Type` de
mensaje.

---

## 3. Recepción de mensajes — Webhook

`ArjuyWhatsApp` implementa el flujo completo de recepción de mensajes vía webhook: el verify
challenge (`GET`), la validación de la firma HMAC-SHA256 del `POST` (`X-Hub-Signature-256`), el
parseo del payload de Meta (`entry[].changes[].value.messages[]`), y la entrega del mensaje ya
parseado a tu aplicación por **dos mecanismos que conviven**:

- **Evento** `IArjuyWhatsAppClient.MessageReceived` — para suscripciones livianas/ad-hoc (por
  ejemplo, loguear o reenviar a otro sistema).
- **DI**: implementaciones de `IWhatsAppMessageHandler` registradas en el contenedor — para
  lógica de negocio que necesite resolver otros servicios `Scoped` (un `DbContext`, por ejemplo).

Tu controller solo tiene que llamar a `VerifyWebhookChallenge` (en el `GET`) y a
`ProcessWebhookAsync` (en el `POST`) — la librería hace el resto. Esta sección te muestra cómo.

### 3.1. Por qué hace falta exponer un endpoint público

WhatsApp Cloud API funciona con un modelo de *webhook*: cuando alguien te escribe, o cuando
cambia el estado de un mensaje que enviaste (entregado, leído, fallido), **Meta le pega a una URL
tuya** con un `POST` HTTP. Para que eso funcione, esa URL tiene que:

- Ser accesible por HTTPS desde internet (no alcanza con `https://localhost:5001`, porque
  los servidores de Meta no tienen forma de llegar a tu máquina).
- Tener un certificado TLS válido (HTTPS es obligatorio, no opcional).

En producción esto lo resuelve tu hosting habitual (Azure, un VPS con reverse proxy, etc.). En
desarrollo local, donde no tenés una URL pública, se resuelve con un túnel (ver sección 3.5,
ngrok).

### 3.2. Configuración en Meta for Developers

En el panel de [Meta for Developers](https://developers.facebook.com/), dentro de tu app, en la
sección **WhatsApp > Configuration** (o **Webhooks**, según la versión del panel), vas a encontrar
dos campos a completar:

- **Callback URL**: la URL pública de tu endpoint (ej.
  `https://miapi.com/api/webhooks/whatsapp`).
- **Verify Token**: un string arbitrario que vos elegís — tiene que coincidir exactamente con el
  valor que pongas en `ArjuyWhatsAppOptions.VerifyToken`.

Al guardar, Meta va a hacer un `GET` de verificación contra tu Callback URL (el handshake descripto
en 3.3) antes de aceptar la configuración. Si ese `GET` no responde como Meta espera, el panel
rechaza la URL.

También tenés que suscribirte a los campos de webhook que te interesan (típicamente `messages`)
en la misma sección.

### 3.3. Registrar el webhook — `MapArjuyWhatsAppWebhook` (recomendado)

La forma recomendada de exponer el webhook es dejar que la librería registre la ruta por vos, con
una sola línea en `Program.cs`, **después** de `builder.Build()`:

```csharp
using ArjuyWhatsApp;

var app = builder.Build();

app.MapArjuyWhatsAppWebhook(); // registra GET+POST en /api/webhooks/whatsapp por default

app.Run();
```

Con esa línea sola ya tenés andando, sin escribir ningún Controller:

- **`GET /api/webhooks/whatsapp`** — el handshake de verificación que Meta hace al configurar o
  re-verificar la Callback URL. Lee `hub.mode`, `hub.verify_token` y `hub.challenge` de la query
  string, llama a `VerifyWebhookChallenge` internamente, y responde el `hub.challenge` como texto
  plano con `200 OK` si el token coincide, o `403 Forbidden` si no.
- **`POST /api/webhooks/whatsapp`** — la recepción de mensajes/eventos entrantes. Lee el body crudo
  y el header `X-Hub-Signature-256`, llama a `ProcessWebhookAsync` internamente, y responde
  `200 OK` si la firma es válida (aunque algún mensaje individual haya fallado río abajo — ver
  sección 4), o `403 Forbidden` si la firma no es válida.

Si querés un path distinto a `/api/webhooks/whatsapp` (por ejemplo, porque ya usás ese path para
otra cosa), pasalo como parámetro:

```csharp
app.MapArjuyWhatsAppWebhook("/webhooks/wa");
```

Esto es exactamente lo que hace `ArjuyWhatsApp.Sample.Api` — mirá su `Program.cs`.

#### Si necesitás más control

`MapArjuyWhatsAppWebhook` cubre el caso general, pero es una capa fina sobre dos métodos que siguen
siendo públicos a propósito: `VerifyWebhookChallenge` y `ProcessWebhookAsync`. Si necesitás lógica
adicional en el endpoint (autenticación propia, logging particular, devolver otro status code,
correlacionar con otro middleware, etc.), no uses `MapArjuyWhatsAppWebhook` — armá tu propio
Controller (o tu propio endpoint de Minimal API) llamando a esos dos métodos directamente:

```csharp
using Microsoft.AspNetCore.Mvc;
using ArjuyWhatsApp;

[ApiController]
[Route("api/webhooks/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;

    public WhatsAppWebhookController(IArjuyWhatsAppClient whatsAppClient)
    {
        _whatsAppClient = whatsAppClient;
    }

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string verifyToken,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var result = _whatsAppClient.VerifyWebhookChallenge(mode, verifyToken, challenge);

        if (result is null)
        {
            // StatusCode(403) en vez de Forbid(): Forbid() delega en el esquema de autenticación
            // configurado con AddAuthentication(...), y explota con un 500
            // (InvalidOperationException: "No authenticationScheme was specified...") si tu API no
            // tiene ninguno registrado — que es el caso típico de una API que solo expone este
            // webhook. StatusCode(403) no depende de eso.
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return Content(result, "text/plain");
    }
}
```

`VerifyWebhookChallenge` compara `mode` contra `"subscribe"` y `verifyToken` contra
`ArjuyWhatsAppOptions.VerifyToken` internamente — tu controller no necesita conocer esa lógica ni
inyectar `IOptions<ArjuyWhatsAppOptions>` para esto. Si el token no coincide, devuelve `null` y hay
que responder con un status que no sea 200 (por ejemplo `403 Forbidden`, como en el ejemplo) — Meta
interpreta cualquier respuesta que no sea el `hub.challenge` en texto plano con `200 OK` como una
verificación fallida.

> **Ojo con `Forbid()`**: si tu API no tiene ningún esquema de autenticación registrado
> (`AddAuthentication(...)`) — el caso normal de una API que solo expone este webhook — usar el
> helper `Forbid()` de ASP.NET Core tira un `500` en vez de un `403`, porque internamente busca un
> `DefaultForbidScheme` que no existe. Usá `StatusCode(StatusCodes.Status403Forbidden)` en su lugar,
> como en el ejemplo de arriba. `MapArjuyWhatsAppWebhook` ya lo maneja así por vos.

### 3.4. Recepción del `POST` entrante — `ProcessWebhookAsync`

Cada `POST` que Meta manda a tu webhook con los mensajes/eventos reales viene con un header
`X-Hub-Signature-256`, cuyo valor es `sha256=<hmac-hex>`, calculado con tu `AppSecret` (el de la
app de Meta, **no** el `AccessToken`) como clave sobre el **body crudo** (los bytes exactos tal
cual llegaron, antes de deserializar a JSON). `ProcessWebhookAsync` valida esa firma, parsea el
payload, y por cada mensaje entrante dispara `MessageReceived` e invoca a cada
`IWhatsAppMessageHandler` registrado — todo en un solo llamado. Esto es exactamente lo que hace
`MapArjuyWhatsAppWebhook` en su `POST` (sección 3.3); si armás tu propio Controller a mano en lugar
de usarlo, el equivalente es:

```csharp
using System.Text;
using Microsoft.AspNetCore.Mvc;
using ArjuyWhatsApp;

[ApiController]
[Route("api/webhooks/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;

    public WhatsAppWebhookController(IArjuyWhatsAppClient whatsAppClient)
    {
        _whatsAppClient = whatsAppClient;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        // Habilitar buffering para poder leer el body crudo sin romper el model binding.
        Request.EnableBuffering();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var signatureHeader = Request.Headers["X-Hub-Signature-256"].ToString();

        var result = await _whatsAppClient.ProcessWebhookAsync(rawBody, signatureHeader, cancellationToken);

        if (!result.IsSuccess)
        {
            // Firma inválida o payload no parseable — no se procesó nada. Ver result.Message.
            return Unauthorized();
        }

        // Siempre devolvé 200 a Meta una vez validada la firma, aunque algún mensaje individual
        // haya fallado río abajo (los handlers ya loguean sus propias excepciones — ver 4.).
        return Ok();
    }
}
```

Notas:

- `ProcessWebhookAsync` valida la firma internamente con `CryptographicOperations.FixedTimeEquals`
  (para evitar timing attacks) — tu controller no calcula ningún HMAC a mano.
- `Request.EnableBuffering()` sigue siendo necesario para leer el body crudo antes del model
  binding, exactamente igual que si lo hicieras vos mismo.
- Si `signatureHeader` viene vacío o `AppSecret` no está configurado, `ProcessWebhookAsync` devuelve
  `MResult<bool>.Fail(...)` sin procesar nada — nunca asume que un webhook sin firma es válido.

### 3.4.1. Suscribirse al evento `MessageReceived`

Para lógica liviana/ad-hoc (por ejemplo, un log central o reenviar a otro sistema), suscribite al
evento en `Program.cs` **después** de construir el `IServiceProvider`, resolviendo el cliente como
lo que es: un singleton (ver `ServiceCollectionExtensions.RegisterClient`, que lo registra con
`AddSingleton` a propósito, justamente para que esta suscripción no se pierda entre requests).

```csharp
using ArjuyWhatsApp;

var app = builder.Build();

// El cliente es Singleton: esta suscripción vive durante toda la vida de la app, no por request.
var whatsAppClient = app.Services.GetRequiredService<IArjuyWhatsAppClient>();
whatsAppClient.MessageReceived += (sender, args) =>
{
    var message = args.Message;
    Console.WriteLine($"[WhatsApp] Mensaje de {message.From} ({message.Type}): {message.Text}");
};

app.Run();
```

### 3.4.2. Implementar un `IWhatsAppMessageHandler` por DI

Para lógica de negocio real (guardar el mensaje, resolver un `DbContext`, disparar otros
servicios), implementá `IWhatsAppMessageHandler` y registralo como `Scoped` — `ProcessWebhookAsync`
crea un scope nuevo por mensaje para resolverlo correctamente aunque el cliente sea `Singleton`:

```csharp
using ArjuyWhatsApp;

public class GuardarMensajeHandler : IWhatsAppMessageHandler
{
    private readonly MiDbContext _dbContext;

    public GuardarMensajeHandler(MiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(WhatsAppMessageReceived message, CancellationToken cancellationToken = default)
    {
        _dbContext.MensajesEntrantes.Add(new MensajeEntrante
        {
            From = message.From,
            MessageId = message.MessageId,
            Texto = message.Text,
            MediaId = message.MediaId,
            Timestamp = message.Timestamp
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

```csharp
// Program.cs — antes de builder.Build()
builder.Services.AddScoped<IWhatsAppMessageHandler, GuardarMensajeHandler>();
```

Podés registrar varios `IWhatsAppMessageHandler` — `ProcessWebhookAsync` los invoca a todos para
cada mensaje. Si uno lanza una excepción, se loguea y se sigue con los demás (y con el resto del
webhook) — un handler roto no tumba a los otros.

Si el mensaje trae `MediaId` (imagen o documento), descargá el binario con
`DownloadMediaAsync(message.MediaId)` (ver sección 2.5) desde dentro de tu handler.

### 3.4.3. Estados de entrega — `MessageStatusUpdated` / `IWhatsAppStatusHandler`

Además de mensajes entrantes, Meta manda por el **mismo webhook** (mismo endpoint, mismo `POST`)
actualizaciones de estado de los mensajes que vos mandaste: `sent` → `delivered` → `read`, o
`failed` si no se pudo entregar. `ProcessWebhookAsync` distingue automáticamente si el payload
trae `messages[]` (mensaje entrante, sección anterior) o `statuses[]` (estado de entrega, esta
sección) — un webhook de Meta trae uno u otro, nunca ambos a la vez.

El mecanismo es el mismo que para mensajes entrantes, pero con tipos separados a propósito:
`WhatsAppMessageStatusUpdate` en vez de `WhatsAppMessageReceived`, evento `MessageStatusUpdated` en
vez de `MessageReceived`, e interfaz `IWhatsAppStatusHandler` en vez de `IWhatsAppMessageHandler` —
son eventos de dominio distintos (un mensaje que llega vs. la confirmación de entrega de uno que
vos mandaste) y mezclarlos en una sola interfaz obligaría a todo handler a discriminar con un `if`
qué caso le tocó.

**Opción A — suscribirse al evento (lógica liviana/ad-hoc):**

```csharp
using ArjuyWhatsApp;

var app = builder.Build();

var whatsAppClient = app.Services.GetRequiredService<IArjuyWhatsAppClient>();
whatsAppClient.MessageStatusUpdated += (sender, args) =>
{
    var status = args.StatusUpdate;
    Console.WriteLine($"[WhatsApp] Mensaje {status.MessageId} → {status.Status}");

    if (status.Status == WhatsAppMessageStatus.Failed)
    {
        Console.WriteLine($"  Error {status.ErrorCode}: {status.ErrorMessage}");
    }
};

app.Run();
```

**Opción B — implementar `IWhatsAppStatusHandler` por DI (lógica de negocio real):**

```csharp
using ArjuyWhatsApp;

public class ActualizarEstadoNotificacionHandler : IWhatsAppStatusHandler
{
    private readonly MiDbContext _dbContext;

    public ActualizarEstadoNotificacionHandler(MiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(WhatsAppMessageStatusUpdate status, CancellationToken cancellationToken = default)
    {
        var notificacion = await _dbContext.Notificaciones
            .FirstOrDefaultAsync(n => n.MessageId == status.MessageId, cancellationToken);

        if (notificacion is null)
        {
            return;
        }

        notificacion.Estado = status.Status.ToString();
        notificacion.ErrorCode = status.ErrorCode;
        notificacion.ErrorMessage = status.ErrorMessage;
        notificacion.ActualizadoEn = status.Timestamp;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

```csharp
// Program.cs — antes de builder.Build()
builder.Services.AddScoped<IWhatsAppStatusHandler, ActualizarEstadoNotificacionHandler>();
```

Igual que con `IWhatsAppMessageHandler`: podés registrar varios `IWhatsAppStatusHandler`, se
invocan todos por cada estado recibido, y si uno lanza una excepción se loguea y se sigue con los
demás sin tumbar el resto del webhook.

### 3.5. Desarrollo local con ngrok

Mientras desarrollás, tu API corre en `https://localhost:<puerto>` — Meta no puede llegar ahí. La
forma más simple de exponerla temporalmente a internet es con [ngrok](https://ngrok.com/).

**Paso 1 — Instalar ngrok**

- Descarga directa: [ngrok.com/download](https://ngrok.com/download).
- En Windows, si tenés Chocolatey instalado:

  ```powershell
  choco install ngrok
  ```

**Paso 2 — Autenticarte**

Necesitás una cuenta gratuita en [ngrok.com](https://ngrok.com/) para obtener un authtoken. Una
vez que lo tengas:

```bash
ngrok config add-authtoken <tu-authtoken>
```

Esto se hace una sola vez por máquina.

**Paso 3 — Levantar el túnel**

Con tu API ASP.NET Core corriendo localmente (por ejemplo en el puerto `7001` — reemplazá por el
puerto real que use tu proyecto, el que ves en `launchSettings.json` o en la consola al hacer
`dotnet run`):

```bash
ngrok http https://localhost:7001
```

ngrok te va a mostrar en la terminal una URL pública del estilo:

```
Forwarding    https://xxxx-xx-xx-xx-xx.ngrok-free.app -> https://localhost:7001
```

**Paso 4 — Configurar esa URL en Meta**

Tomá la URL pública que te dio ngrok y agregale el path de tu endpoint de webhook. Si usás
`MapArjuyWhatsAppWebhook()` con el pattern por default (o un controller propio mapeado en
`api/webhooks/whatsapp`, como en los ejemplos de arriba), la Callback URL que cargás en el panel de
Meta (sección 3.2) sería:

```
https://xxxx-xx-xx-xx-xx.ngrok-free.app/api/webhooks/whatsapp
```

Guardá esa URL junto con tu `Verify Token` — Meta va a disparar el handshake de la sección 3.3
contra esa URL pública.

**Importante — la URL gratuita cambia en cada reinicio**

Con la cuenta gratuita de ngrok, cada vez que cortás y volvés a levantar el túnel (`ngrok http
...`), te da una URL nueva y distinta. Eso significa que tenés que volver a actualizar la Callback
URL en el panel de Meta cada vez que reiniciás ngrok durante el desarrollo.

Si tenés un dominio fijo reservado en ngrok (plan pago, o el dominio estático gratuito que ngrok
ofrece por cuenta), podés fijarlo así y evitarte reconfigurar Meta en cada reinicio:

```bash
ngrok http --domain=tu-dominio-fijo.ngrok-free.app 7001
```

**Nota de seguridad**

Mientras el túnel de ngrok está activo, tu máquina (o al menos el puerto que expusiste) es
accesible desde internet. No dejes el túnel corriendo cuando no estés probando activamente el
webhook — cerralo (`Ctrl+C` en la terminal de ngrok) al terminar la sesión de trabajo.

---

## 4. Troubleshooting común

| Síntoma | Causa probable | Cómo diagnosticar / resolver |
|---|---|---|
| `MResult.Fail` con mensaje de error de autenticación (401/403 de Meta) al enviar cualquier mensaje | `AccessToken` vencido o revocado. Los tokens de usuario de corta duración expiran en ~1-2 hs; incluso los de larga duración (60 días) o de System User pueden haber sido revocados desde el panel de Meta. | Generá un nuevo token en **Meta for Developers > WhatsApp > API Setup** (o desde tu System User, si usás uno de larga duración) y actualizá `AccessToken` en tu configuración. Revisá el `Message` del `MResult` — Meta suele indicar `"Error validating access token"` explícitamente. |
| Error indicando que el número de teléfono / recurso no existe, o `phone_number_id` inválido | `PhoneNumberId` incorrecto, o pertenece a otra WABA/app distinta a la del `AccessToken` usado. | Confirmá el Phone Number ID exacto en **Meta for Developers > WhatsApp > API Setup** (es un ID numérico largo, no el número de teléfono en sí). Verificá que el `AccessToken` tenga permisos sobre esa misma WABA (`BusinessAccountId`). |
| `SendTemplateAsync` falla indicando que la plantilla no existe o no está aprobada | La plantilla (`templateName`) todavía está en revisión, fue rechazada, o el `languageCode` no coincide con el idioma exacto en que fue aprobada (ej. pediste `"es"` pero la plantilla se aprobó como `"es_AR"`). | Revisá el estado de la plantilla en **Meta for Developers > WhatsApp > Message Templates** — el estado tiene que figurar como "Approved". Verificá que `languageCode` coincida carácter por carácter con el idioma configurado en la plantilla aprobada. |
| Tu controller devuelve `401`/`Unauthorized` porque `ProcessWebhookAsync` devuelve `IsSuccess = false`, o Meta reporta reintentos fallidos de entrega | La validación interna de `X-Hub-Signature-256` (sección 3.4) está fallando: `AppSecret` incorrecto, o el `rawBody` que le pasás a `ProcessWebhookAsync` no es el body crudo exacto (por ejemplo, si lo deserializaste primero y volviste a serializar antes de llamarlo, el resultado no va a coincidir byte a byte). | Confirmá que `AppSecret` en tu configuración coincida exactamente con el "App Secret" del panel de Meta (sección **App Settings > Basic**, no confundir con el `AccessToken`). Asegurate de pasarle a `ProcessWebhookAsync` el string leído con `Request.EnableBuffering()` **antes** de cualquier deserialización, no un objeto re-serializado. Revisá `result.Message` para el detalle exacto del fallo. |
| El handshake de verificación (`GET`) falla al configurar la Callback URL en Meta | `VerifyToken` no coincide entre lo cargado en el panel de Meta y `ArjuyWhatsAppOptions.VerifyToken`, o el controller no está devolviendo `hub.challenge` como texto plano (`text/plain`) con `200 OK`. | Comparar el `Verify Token` tipeado en el panel de Meta contra el valor real de la opción (cuidado con espacios extra al copiar/pegar). Confirmar que la respuesta del `GET` sea el string de `hub.challenge` sin comillas ni envoltorio JSON — usar `Content(challenge, "text/plain")` como en el ejemplo de la sección 3.3, no `Ok(challenge)` (que lo envuelve distinto según el content negotiation configurado). |
| Meta no llega nunca a tu webhook en desarrollo local | Callback URL desactualizada por reinicio de ngrok (la URL gratuita cambia cada vez, ver sección 3.5), o el túnel de ngrok no está corriendo. | Verificá que ngrok siga activo (`ngrok http ...` corriendo en una terminal) y que la Callback URL cargada en Meta coincida con la URL pública actual que muestra ngrok. |

---

## 5. Guía de prueba end-to-end

Esta sección te lleva paso a paso, en orden, para probar la librería de punta a punta con el
**número de prueba de WhatsApp Cloud API de Meta**: mandar un mensaje y — lo más importante —
recibir la respuesta vía webhook, usando ngrok para exponer tu máquina a internet.

Para esto usás `ArjuyWhatsApp.Sample.Api` (no `ArjuyWhatsApp.Sample`, la consola): recibir un
webhook requiere un servidor HTTP real escuchando una URL pública, y una app de consola no puede
exponer eso. `ArjuyWhatsApp.Sample.Api` ya trae:

- El webhook real, registrado con una sola línea (`app.MapArjuyWhatsAppWebhook();` en
  `ArjuyWhatsApp.Sample.Api/Program.cs`), tal como se documenta en la sección 3.3 — sin Controller
  propio.
- Endpoints de envío para probar sin Postman aparte (`Controllers/WhatsAppSendController.cs`):
  `POST api/send/text`, `POST api/send/template`, `POST api/send/image`, `POST api/send/document`.
- Los dos mecanismos de recepción activos a la vez (evento `MessageReceived` y un
  `IWhatsAppMessageHandler` por DI, `LoggingMessageHandler`), cada uno logueando con un prefijo
  distinto (`[EVENTO MessageReceived]` vs `[DI IWhatsAppMessageHandler]`) para que se vea
  claramente que ambos se disparan para el mismo mensaje — ver `Program.cs` de ese proyecto.

### Paso 1 — Configurar credenciales reales

Editá `ArjuyWhatsApp.Sample.Api/appsettings.json` (o mejor, `appsettings.Development.json` /
`dotnet user-secrets`, ver la nota de seguridad de la sección 1.2) con los datos reales de tu
**número de prueba** de Meta (Meta for Developers > tu app > WhatsApp > API Setup):

```json
{
  "ArjuyWhatsApp": {
    "AccessToken": "tu-token-real",
    "PhoneNumberId": "tu-phone-number-id-real",
    "BusinessAccountId": "tu-business-account-id-real",
    "AppSecret": "tu-app-secret-real",
    "VerifyToken": "elegí-cualquier-string-vos-mismo",
    "ApiVersion": "v21.0"
  }
}
```

Recordá: `VerifyToken` no te lo da Meta, lo inventás vos (sección 1.2) y tiene que coincidir con
lo que cargues en el panel de Meta en el paso 4.

### Paso 2 — Levantar `ArjuyWhatsApp.Sample.Api`

```bash
dotnet run --project ArjuyWhatsApp.Sample.Api
```

El proyecto trae un puerto HTTPS fijo en `Properties/launchSettings.json`: **`https://localhost:7100`**
(HTTP en `http://localhost:5100`). Confirmá en la consola que arrancó sin excepciones y que
escucha en ese puerto.

### Paso 3 — Levantar ngrok apuntando a ese puerto

Con el puerto fijo del paso 2, el comando puntual (ver sección 3.5 para instalación/autenticación
de ngrok, que no se repite acá):

```bash
ngrok http https://localhost:7100
```

Copiá la URL pública que te muestra ngrok (`https://xxxx-xx-xx-xx-xx.ngrok-free.app`).

### Paso 4 — Configurar el webhook en el panel de Meta

En **Meta for Developers > tu app > WhatsApp > Configuration** (número de prueba), cargá:

- **Callback URL**: `https://xxxx-xx-xx-xx-xx.ngrok-free.app/api/webhooks/whatsapp` (la URL de
  ngrok del paso 3 + el path del controller).
- **Verify Token**: el mismo valor exacto que pusiste en `VerifyToken` en el paso 1.

Guardá — Meta va a hacer el handshake `GET` (sección 3.3) contra esa URL. Si `ArjuyWhatsApp.Sample.Api`
sigue corriendo y ngrok sigue activo, debería aceptarse. Suscribite también al campo `messages`
si el panel te lo pide (sección 3.2).

### Paso 5 — Mandar un mensaje de prueba

Los números de prueba de Meta solo pueden mandar mensajes a números **pre-autorizados** en el
panel (sección **API Setup > To**, agregá y verificá tu propio celular ahí primero). Con eso
hecho, mandate un mensaje a vos mismo con `curl` o Postman contra `ArjuyWhatsApp.Sample.Api`:

```bash
curl -X POST https://localhost:7100/api/send/text \
  -H "Content-Type: application/json" \
  -d "{\"phoneNumber\":\"5491100000000\",\"message\":\"Hola desde ArjuyWhatsApp!\"}"
```

(reemplazá `5491100000000` por tu número real pre-autorizado). La respuesta es el `MResult<string>`
como JSON — revisá `isSuccess`/`message` si algo falla (ver troubleshooting, sección 4).

### Paso 6 — Contestar desde tu WhatsApp y ver los dos logs

Deberías recibir el mensaje del paso 5 en tu WhatsApp real. Contestalo. Si el webhook está bien
configurado, en la consola donde corre `ArjuyWhatsApp.Sample.Api` vas a ver **dos líneas** para el
mismo mensaje entrante, una por cada mecanismo de recepción activo a la vez:

```
info: MessageReceivedEvent[0]
      [EVENTO MessageReceived] Mensaje de 5491100000000 (Text, id wamid.XXX): tu respuesta
info: LoggingMessageHandler[0]
      [DI IWhatsAppMessageHandler] Mensaje de 5491100000000 (Text, id wamid.XXX): tu respuesta
```

Eso confirma que el evento `MessageReceived` y el `IWhatsAppMessageHandler` registrado por DI
(sección 3.4.1 y 3.4.2) se disparan juntos para el mismo mensaje, tal como documenta la librería.

### Paso 7 — Si no llega nada

Antes de nada, revisá la tabla de troubleshooting de la sección 4 — en particular las filas sobre
firma inválida, handshake fallido, y "Meta no llega nunca a tu webhook en desarrollo local"
(la causa más común: ngrok se reinició y la Callback URL en Meta quedó desactualizada — sección 3.5).

---

## 6. Debugging con Visual Studio

Esta sección explica cómo debuggear con breakpoints, en **Visual Studio** (no VS Code ni Rider),
los dos flujos de `ArjuyWhatsApp.Sample.Api`: el ENVÍO (a través de `WhatsAppSendController`) y la
RECEPCIÓN (el webhook mapeado con `app.MapArjuyWhatsAppWebhook()`).

`ArjuyWhatsApp.Sample.Api` ya trae Swagger/OpenAPI (`Swashbuckle.AspNetCore`) habilitado en
`Program.cs` (`AddSwaggerGen` + `UseSwagger`/`UseSwaggerUI`), así que no hace falta Postman ni
`curl` aparte para probar el envío mientras se debuggea.

1. Abrí `ArjuyWhatsApp.sln` en Visual Studio.
2. En el **Solution Explorer**, click derecho sobre `ArjuyWhatsApp.Sample.Api` → **"Set as
   Startup Project"** (el formato `.sln` no persiste de forma simple el proyecto de inicio único
   entre máquinas/clones, así que este paso es manual cada vez que abrís el repo en una PC nueva).
3. Poné un breakpoint en la línea que ya tiene el comentario `🔴 PONÉ UN BREAKPOINT ACÁ`, según qué
   quieras inspeccionar:
   - **Envío** — `ArjuyWhatsApp.Sample.Api/Controllers/WhatsAppSendController.cs`, en cada uno de
     los 4 métodos (`SendText`, `SendTemplate`, `SendImage`, `SendDocument`): el comentario está
     justo antes de la llamada a `_whatsAppClient.SendXxxAsync(...)` — parate ahí para ver el
     `request` recibido, y avanzá una línea (F10) para ver el `MResult<string>` que devuelve la
     librería.
   - **Recepción, mecanismo evento** — `ArjuyWhatsApp.Sample.Api/Program.cs`, dentro del handler
     `whatsAppClient.MessageReceived += (sender, args) => { ... }`.
   - **Recepción, mecanismo DI** — `ArjuyWhatsApp.Sample.Api/Program.cs`, en el método
     `HandleAsync` de la clase `LoggingMessageHandler` (definida al final del mismo archivo,
     `Program.cs`, no en un archivo separado).
4. Apretá **F5** (o el botón verde ▶ Run). Se levanta el servidor y, gracias a Swagger, el
   navegador abre directo en `/swagger` (configurado en `Properties/launchSettings.json` con
   `"launchBrowser": true` y `"launchUrl": "swagger"`).
5. Para probar **ENVÍO**: desde Swagger UI, expandí `POST /api/send/text`, click en "Try it out",
   completá el body de ejemplo (`{"phoneNumber": "5491100000000", "message": "Hola"}`) y ejecutá.
   Visual Studio va a pausar en el breakpoint del Controller — revisá `request` y, un paso después,
   el `result` (`MResult<string>`) en la ventana de **Locals**/**Autos**.
6. Para probar **RECEPCIÓN**: con ngrok corriendo y el webhook configurado en Meta (ver sección 3.5
   y la Guía de prueba end-to-end de la sección 5 — no se repite acá), mandate un WhatsApp real al
   número de prueba. Visual Studio va a pausar en los dos breakpoints del punto 3 (evento y
   handler), uno después del otro, para el mismo mensaje — ahí podés inspeccionar el objeto
   `WhatsAppMessageReceived` completo (`From`, `Text`, `Type`, `MessageId`, etc.) en el debugger.
7. Tip de Visual Studio: usá **Debug > Windows > Locals** para ver todas las variables en foco sin
   buscarlas, y agregá un **Watch** (click derecho sobre una variable → "Add Watch", o `Debug >
   Windows > Watch`) sobre un campo puntual (por ejemplo `message.Text` o `result.IsSuccess`) si
   querés seguirlo entre varios breakpoints sin tener que reabrir el árbol de Locals cada vez.

---

## 7. Otros ejemplos de implementación

Además de `ArjuyWhatsApp.Sample` (consola) y `ArjuyWhatsApp.Sample.Api` (usado en la guía end-to-end
de la sección 5), el repositorio trae tres samples más, cada uno mostrando la librería integrada en
un tipo de aplicación distinto: **Razor Pages** (`Sample.Web`), **Windows Forms** (`Sample.WinForms`)
y **WPF** (`Sample.Wpf`).

Los cuatro samples de "servidor/UI" — `Sample.Api`, `Sample.Web`, `Sample.WinForms` y `Sample.Wpf` —
escuchan a propósito en **puertos distintos** (`7100`, `7220`/`5220`, `7300` y `7200`
respectivamente), justamente para poder tenerlos corriendo todos al mismo tiempo si hace falta
comparar comportamiento entre ellos sin que se pisen los puertos.

### 7.1. Web (`ArjuyWhatsApp.Sample.Web`)

Sample tipo "chat" con **Razor Pages**: una única página (`Pages/Index`) con un form para mandar un
mensaje de texto (`POST`, patrón PRG — sin redirect completo, para poder mostrar el resultado del
envío en la misma respuesta) y, debajo, el listado de mensajes recibidos hasta el momento, leídos
desde un `IMessageStore` en memoria (`InMemoryMessageStore`, poblado por `StoringMessageHandler`, un
`IWhatsAppMessageHandler` registrado por DI). Igual que en `Sample.Api`, conviven a propósito los dos
mecanismos de recepción de la librería (el handler por DI y el evento `MessageReceived`, este último
solo para loguear).

Para correrlo:

```bash
dotnet run --project ArjuyWhatsApp.Sample.Web
```

Levanta en `https://localhost:7220` (HTTP en `http://localhost:5220`, ver
`Properties/launchSettings.json`). El webhook queda mapeado en `/api/webhooks/whatsapp` con
`MapArjuyWhatsAppWebhook()`, igual que en los demás samples — para probar la recepción con Meta
real, apuntá ngrok a `7220`.

### 7.2. WinForms (`ArjuyWhatsApp.Sample.WinForms`)

Sample de escritorio (**Windows Forms**) con una ventana (`Form1`) que manda un mensaje de texto y
muestra en un `ListBox` los mensajes recibidos, igual que los demás samples.

**La particularidad**: WinForms no tiene forma nativa de exponer un endpoint HTTP, y Meta necesita
poder pegarle a una URL (GET para el verify challenge, POST para cada mensaje entrante). La solución
es levantar un mini-host **ASP.NET Core (Kestrel) embebido** dentro del mismo proceso de WinForms,
corriendo en background (`Task.Run(() => webHost.RunAsync())`), con `MapArjuyWhatsAppWebhook()`
atendiendo `/api/webhooks/whatsapp` — sin UI propia, sin servir páginas, conviviendo con el `Form`
principal en el mismo proceso.

Ese host embebido escucha en el **puerto fijo 7300** (`builder.WebHost.UseUrls("http://localhost:7300")`
en `Program.cs`). Como `MessageReceived` se dispara desde un thread del pool de Kestrel — nunca el
UI thread de WinForms — la suscripción hace el marshaling correcto con `this.Invoke`/`this.BeginInvoke`
antes de tocar el `ListBox` (ver `Form1.OnMessageReceived`); tocarlo directo desde ese thread tira una
`InvalidOperationException` de cross-thread.

Para correrlo (requiere Windows):

```bash
dotnet run --project ArjuyWhatsApp.Sample.WinForms
```

**Importante para probar el webhook con ESTE sample**: ngrok tiene que apuntar al **7300**, no al
7100 de `Sample.Api` ni a ningún otro puerto de los demás samples:

```bash
ngrok http 7300
```

### 7.3. WPF (`ArjuyWhatsApp.Sample.Wpf`)

Mismo concepto que `Sample.WinForms`, pero en **WPF**: una `MainWindow` con form de envío y una
lista de recibidos bindeada a una `ObservableCollection<WhatsAppMessageReceived>`.

También levanta un host Kestrel embebido en background (`_webHost.RunAsync()` desde
`App.xaml.cs`, en `OnStartup`), con `MapArjuyWhatsAppWebhook()` en `/api/webhooks/whatsapp`, esta vez
en el **puerto fijo 7200** (`builder.WebHost.UseUrls("http://localhost:7200")`). Como `MessageReceived`
se dispara desde un thread del pool de Kestrel y no desde el Dispatcher thread de WPF, la suscripción
usa `Dispatcher.Invoke` antes de modificar la `ObservableCollection` bindeada (ver
`MainWindow.OnMessageReceived`) — sin eso, WPF tira excepción por acceder a un objeto bindeado a la UI
desde un thread que no es el suyo.

Para correrlo (requiere Windows):

```bash
dotnet run --project ArjuyWhatsApp.Sample.Wpf
```

Para probar el webhook con este sample, ngrok apunta al **7200**:

```bash
ngrok http 7200
```

---

## Referencias

- `README.md` — quickstart corto y roadmap completo de funcionalidades pendientes.
- `ArjuyWhatsApp/ArjuyWhatsAppOptions.cs` — definición de todas las opciones de configuración.
- `ArjuyWhatsApp/ServiceCollectionExtensions.cs` — los dos métodos de registro (`AddArjuyWhatsApp`).
- `ArjuyWhatsApp/IArjuyWhatsAppClient.cs` — contrato completo del cliente, incluyendo `MessageReceived`, `VerifyWebhookChallenge` y `ProcessWebhookAsync`.
- `ArjuyWhatsApp/WebhookEndpointExtensions.cs` — `MapArjuyWhatsAppWebhook`, el método de Minimal API que registra el webhook en una sola línea (ver sección 3.3).
- `ArjuyWhatsApp/WhatsAppMessageReceived.cs` — modelo de mensaje entrante parseado, y `WhatsAppMessageType`.
- `ArjuyWhatsApp/IWhatsAppMessageHandler.cs` — contrato para manejar mensajes entrantes por DI.
- `ArjuyWhatsApp/MResult.cs` — definición de `MResult` / `MResult<T>`.
- `ArjuyWhatsApp.Sample/Program.cs` — ejemplo ejecutable de registro y envío (consola, menú interactivo).
- `ArjuyWhatsApp.Sample.Api/Program.cs` — sample con servidor HTTP real: registro, los dos mecanismos de recepción activos, y controllers de envío/webhook (ver sección 5, Guía de prueba end-to-end).
