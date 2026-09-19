# ArjuyWhatsApp

Cliente .NET liviano y sin dependencias de terceros propias para la **WhatsApp Cloud API** oficial
de Meta. Permite enviar mensajes de texto, plantillas, imágenes y documentos, y descargar media
recibida, usando `HttpClient` a través de `IHttpClientFactory` e integración estándar con
`Microsoft.Extensions.DependencyInjection` / `Microsoft.Extensions.Options`.

No envuelve ningún SDK no oficial: habla directamente contra `https://graph.facebook.com`.

## Instalación

```bash
dotnet add package ArjuyWhatsApp
```

## Quickstart

### 1. Registrar el cliente

**Opción A — desde `IConfiguration` (recomendada para apps con `appsettings.json`):**

```csharp
using ArjuyWhatsApp;

var builder = Host.CreateApplicationBuilder(args);

// Lee la sección "ArjuyWhatsApp" de appsettings.json (nombre de sección configurable).
builder.Services.AddArjuyWhatsApp(builder.Configuration);
```

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

**Opción B — configuración en código, sin `IConfiguration`:**

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
> (`IArjuyWhatsAppClient`) y la última en ejecutarse gana la configuración de opciones.

### 2. Enviar un mensaje de texto

```csharp
var whatsAppClient = serviceProvider.GetRequiredService<IArjuyWhatsAppClient>();

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

Ver el proyecto `ArjuyWhatsApp.Sample` para un ejemplo ejecutable completo, con ambas formas de
registro comentadas en `Program.cs`.

## API — `IArjuyWhatsAppClient`

| Método / miembro | Descripción |
|---|---|
| `Task<MResult<string>> SendTextAsync(string phoneNumber, string message)` | Envía un mensaje de texto libre. Devuelve el `message id` de Meta en `Data`. |
| `Task<MResult<string>> SendTemplateAsync(string phoneNumber, string templateName, string languageCode, IEnumerable<string> parameters)` | Envía un mensaje basado en una plantilla aprobada, con parámetros posicionales del body. |
| `Task<MResult<string>> SendImageAsync(string phoneNumber, string imageUrl, string? caption = null)` | Envía una imagen por URL pública, con caption opcional. |
| `Task<MResult<string>> SendDocumentAsync(string phoneNumber, string documentUrl, string fileName, string? caption = null)` | Envía un documento por URL pública, con nombre de archivo y caption opcional. |
| `Task<MResult<byte[]>> DownloadMediaAsync(string mediaId)` | Descarga el binario de un media id (por ejemplo, el recibido en un webhook entrante). |
| `Task<MResult<string>> UploadMediaAsync(byte[] fileContent, string fileName, string mimeType)` | Sube un archivo local a Meta (`POST /{phone-number-id}/media`, sin necesidad de URL pública) y devuelve el `media_id` en `Data`. Pensado para archivos privados. |
| `Task<MResult<string>> SendImageByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null)` | Envía una imagen previamente subida con `UploadMediaAsync`, referenciándola por `media_id` en vez de por URL. |
| `Task<MResult<string>> SendDocumentByMediaIdAsync(string phoneNumber, string mediaId, string? fileName = null, string? caption = null)` | Envía un documento previamente subido con `UploadMediaAsync`, referenciándolo por `media_id` en vez de por URL. |
| `Task<MResult<string>> SendInteractiveButtonsAsync(string phoneNumber, string bodyText, IEnumerable<(string Id, string Title)> buttons)` | Envía un mensaje con hasta 3 botones de respuesta rápida. Valida los límites de Meta (máximo 3 botones, título ≤ 20 caracteres, títulos únicos) antes de llamar a la API. Ver `MANUAL.md`, sección "Mensajes interactivos". |
| `Task<MResult<string>> SendInteractiveListAsync(string phoneNumber, string bodyText, string buttonText, IEnumerable<(string SectionTitle, IEnumerable<(string Id, string Title, string? Description)> Rows)> sections)` | Envía un menú desplegable con secciones y filas (hasta 10 filas en total). Valida los límites de Meta antes de llamar a la API. Ver `MANUAL.md`, sección "Mensajes interactivos". |
| `string? VerifyWebhookChallenge(string mode, string verifyToken, string challenge)` | Resuelve el handshake `GET` de verificación del webhook contra `ArjuyWhatsAppOptions.VerifyToken`. |
| `Task<MResult<bool>> ProcessWebhookAsync(string rawBody, string? signatureHeader, CancellationToken cancellationToken = default)` | Valida la firma HMAC del `POST` entrante, parsea el payload de Meta, y entrega cada mensaje vía el evento `MessageReceived`/`IWhatsAppMessageHandler` y cada actualización de estado de entrega vía `MessageStatusUpdated`/`IWhatsAppStatusHandler`. |
| `event EventHandler<WhatsAppMessageReceivedEventArgs>? MessageReceived` | Se dispara por cada mensaje entrante procesado por `ProcessWebhookAsync`. |
| `event EventHandler<WhatsAppMessageStatusUpdateEventArgs>? MessageStatusUpdated` | Se dispara por cada actualización de estado de entrega (`sent`/`delivered`/`read`/`failed`) de un mensaje saliente procesada por `ProcessWebhookAsync`. |

Todos los métodos que devuelven `Task<MResult<T>>` usan el mismo tipo de resultado propio y
autocontenido (`IsSuccess`, `Data`, `Message`), sin excepciones para el flujo normal de error. Las
excepciones de red/HTTP se capturan internamente y se traducen a `MResult.Fail(...)`. Ver
`MANUAL.md`, sección 3, para el flujo completo de recepción de mensajes vía webhook.

## Normalización de números de teléfono

El cliente expone un hook protegido:

```csharp
protected virtual string NormalizePhoneNumber(string phoneNumber)
```

La implementación por defecto solo recorta espacios en blanco — **no** aplica ninguna regla
específica de país (por ejemplo, el "9" móvil de Argentina). Si tu aplicación necesita ese tipo
de normalización, heredá de `ArjuyWhatsAppClient` y sobreescribí el método:

```csharp
public class ArjuyWhatsAppClientArgentina : ArjuyWhatsAppClient
{
    public ArjuyWhatsAppClientArgentina(IHttpClientFactory httpClientFactory, IOptions<ArjuyWhatsAppOptions> options)
        : base(httpClientFactory, options) { }

    protected override string NormalizePhoneNumber(string phoneNumber)
    {
        // Reglas específicas de tu país/proveedor acá.
        return phoneNumber.Trim();
    }
}
```

Y registrá tu clase derivada en el contenedor de DI en lugar de `ArjuyWhatsAppClient`.

## Target framework choice

El paquete apunta a **`net8.0`**.

Se evaluó `netstandard2.0` para maximizar compatibilidad hacia atrás (incluyendo .NET Framework),
pero se descartó porque:

- El SDK instalado en esta máquina (`dotnet --list-sdks` → `10.0.400`) soporta correctamente
  `net8.0`, que es la LTS activa más reciente al momento de crear este paquete.
- `net8.0` da acceso directo a APIs modernas usadas en la implementación (`System.Net.Http.Json`,
  `JsonContent.Create`, `IHttpClientFactory` con `Microsoft.Extensions.Http` 8.x) sin necesidad de
  polyfills ni paquetes adicionales que `netstandard2.0` hubiera requerido.
- El público objetivo (proyectos propios `arjuy*`, todos en .NET moderno) ya vive en .NET 8+, por
  lo que la compatibilidad extendida de `netstandard2.0` no aporta valor real y sí agrega fricción
  (menos APIs disponibles, necesidad de `#if` o paquetes de compatibilidad).

Si en el futuro se necesita soporte para .NET Framework o Xamarin, se puede agregar
`netstandard2.0` como un **multi-targeting** adicional (`<TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>`)
sin romper a los consumidores actuales.

## Roadmap / Pendiente

Estas funcionalidades quedaron **explícitamente fuera de esta primera versión** y son las
primeras candidatas para una futura iteración:

1. **Retry / rate-limiting (HTTP 429)**: Meta puede responder `429 Too Many Requests` con un
   header `Retry-After`. Esta versión no implementa reintentos ni backoff automático — el llamador
   recibe un `MResult.Fail` con el detalle del error y debe manejarlo por su cuenta. Marcado
   con `// TODO:` en `ArjuyWhatsAppClient.SendMessagePayloadAsync`.
2. **Normalización de número de teléfono generalizada**: actualmente solo existe el hook
   `NormalizePhoneNumber` (ver sección de arriba) con una implementación por defecto no-op. No hay
   reglas built-in por país/proveedor — queda a cargo de cada consumidor extender la clase.

## Estructura del repositorio

- `ArjuyWhatsApp/` — la librería empaquetable (el paquete NuGet en sí).
- `ArjuyWhatsApp.Tests/` — tests unitarios con xUnit, mockeando `HttpMessageHandler` (nunca pega
  contra la API real de Meta).
- `ArjuyWhatsApp.Sample/` — consola de ejemplo que muestra ambas formas de registro y un envío de
  texto real usando DI.
- `ArjuyWhatsApp.Sample.Api/` — API mínima con envío por controllers y recepción vía webhook
  (puerto 7100), usada en la guía end-to-end de `MANUAL.md`.
- `ArjuyWhatsApp.Sample.Web/` — sample tipo "chat" con Razor Pages: envío + listado de recibidos
  (puerto 7220/5220).
- `ArjuyWhatsApp.Sample.WinForms/` — sample de escritorio (Windows Forms) con host Kestrel embebido
  para el webhook (puerto 7300).
- `ArjuyWhatsApp.Sample.Wpf/` — sample de escritorio (WPF) con host Kestrel embebido para el webhook
  (puerto 7200).
