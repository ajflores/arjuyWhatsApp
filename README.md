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
| `Task<MResult<string>> SendTemplateAsync(string phoneNumber, string templateName, string languageCode, IEnumerable<string> bodyParameters, WhatsAppTemplateHeaderMedia? headerMedia, IEnumerable<WhatsAppTemplateButtonParameter>? buttonParameters = null)` | Overload de `SendTemplateAsync` con soporte para header dinámico de media (imagen/video/documento, por URL o `media_id`) y/o botones dinámicos (URL con placeholder o quick reply) — para plantillas con solo body, preferí el overload de 4 parámetros. `headerMedia` es obligatorio en este overload (pasá `null` explícito si la plantilla no tiene header dinámico) para que el compilador pueda elegir sin ambigüedad entre los dos overloads cuando se llama con los 4 parámetros básicos. |
| `Task<MResult<string>> SendImageAsync(string phoneNumber, string imageUrl, string? caption = null)` | Envía una imagen por URL pública, con caption opcional. |
| `Task<MResult<string>> SendDocumentAsync(string phoneNumber, string documentUrl, string fileName, string? caption = null)` | Envía un documento por URL pública, con nombre de archivo y caption opcional. |
| `Task<MResult<byte[]>> DownloadMediaAsync(string mediaId)` | Descarga el binario de un media id (por ejemplo, el recibido en un webhook entrante). |
| `Task<MResult<string>> UploadMediaAsync(byte[] fileContent, string fileName, string mimeType)` | Sube un archivo local a Meta (`POST /{phone-number-id}/media`, sin necesidad de URL pública) y devuelve el `media_id` en `Data`. Pensado para archivos privados. |
| `Task<MResult<string>> SendImageByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null)` | Envía una imagen previamente subida con `UploadMediaAsync`, referenciándola por `media_id` en vez de por URL. |
| `Task<MResult<string>> SendDocumentByMediaIdAsync(string phoneNumber, string mediaId, string? fileName = null, string? caption = null)` | Envía un documento previamente subido con `UploadMediaAsync`, referenciándolo por `media_id` en vez de por URL. |
| `Task<MResult<string>> SendInteractiveButtonsAsync(string phoneNumber, string bodyText, IEnumerable<(string Id, string Title)> buttons)` | Envía un mensaje con hasta 3 botones de respuesta rápida. Valida los límites de Meta (máximo 3 botones, título ≤ 20 caracteres, títulos únicos) antes de llamar a la API. Ver `MANUAL.md`, sección "Mensajes interactivos". |
| `Task<MResult<string>> SendInteractiveListAsync(string phoneNumber, string bodyText, string buttonText, IEnumerable<(string SectionTitle, IEnumerable<(string Id, string Title, string? Description)> Rows)> sections)` | Envía un menú desplegable con secciones y filas (hasta 10 filas en total). Valida los límites de Meta antes de llamar a la API. Ver `MANUAL.md`, sección "Mensajes interactivos". |
| `Task<MResult<IReadOnlyList<WhatsAppMessageTemplate>>> GetMessageTemplatesAsync(CancellationToken cancellationToken = default)` | Lista todas las plantillas de mensaje de la cuenta (`ArjuyWhatsAppOptions.BusinessAccountId`), siguiendo automáticamente la paginación de Meta. Cada `WhatsAppMessageTemplate` trae sus componentes (header/body/footer/buttons) y `BodyParameterCount`, para saber cuántos `parameters` pasarle a `SendTemplateAsync` sin ir a copiarlos del panel de Meta. |
| `Task<MResult<bool>> MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default)` | Marca un mensaje entrante como leído (doble tilde azul). Meta solo lo permite dentro de los 30 días de recibido el mensaje. |
| `Task<MResult<bool>> MarkAsReadWithTypingIndicatorAsync(string messageId, CancellationToken cancellationToken = default)` | Igual que `MarkAsReadAsync`, pero además muestra el indicador de "escribiendo..." — Meta lo descarta al responder o a los 25 segundos, lo que ocurra primero. Usar solo si efectivamente se va a responder a continuación. |
| `Task<MResult<string>> SendReactionAsync(string phoneNumber, string messageId, string emoji, CancellationToken cancellationToken = default)` | Reacciona con un emoji a un mensaje. Pasar `emoji: ""` remueve una reacción puesta anteriormente (mecanismo oficial de Meta para "unreact"). No admite `replyToMessageId` — no aplica según Meta. |
| `Task<MResult<string>> SendLocationAsync(string phoneNumber, double latitude, double longitude, string? name = null, string? address = null, string? replyToMessageId = null, CancellationToken cancellationToken = default)` | Envía una ubicación (coordenadas, con nombre y dirección opcionales). |
| `Task<MResult<string>> SendContactsAsync(string phoneNumber, IEnumerable<WhatsAppContact> contacts, string? replyToMessageId = null, CancellationToken cancellationToken = default)` | Envía una o más tarjetas de contacto (`WhatsAppContact`, equivalente a una vCard simplificada, incluye `Urls`). Solo `Name.FormattedName` es obligatorio por contacto — se valida localmente antes de llamar a Meta. |
| `Task<MResult<string>> SendAudioAsync(string phoneNumber, string audioUrl, bool voice = false, string? replyToMessageId = null, CancellationToken cancellationToken = default)` | Envía un audio por URL pública. `voice: true` lo muestra como nota de voz (requiere OGG/Opus mono). Meta NO admite `caption` en audio. |
| `Task<MResult<string>> SendAudioByMediaIdAsync(string phoneNumber, string mediaId, bool voice = false, string? replyToMessageId = null, CancellationToken cancellationToken = default)` | Igual que `SendAudioAsync`, referenciando un audio ya subido con `UploadMediaAsync` por `media_id`. |
| `Task<MResult<string>> SendVideoAsync(string phoneNumber, string videoUrl, string? caption = null, string? replyToMessageId = null, CancellationToken cancellationToken = default)` | Envía un video por URL pública, con caption opcional. |
| `Task<MResult<string>> SendVideoByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null, string? replyToMessageId = null, CancellationToken cancellationToken = default)` | Igual que `SendVideoAsync`, referenciando un video ya subido por `media_id`. |
| `Task<MResult<string>> SendStickerAsync(string phoneNumber, string stickerUrl, string? replyToMessageId = null, CancellationToken cancellationToken = default)` | Envía un sticker por URL pública. Meta exige WebP (estático ≤100KB, animado ≤500KB, no validado localmente). No admite `caption`. |
| `Task<MResult<string>> SendStickerByMediaIdAsync(string phoneNumber, string mediaId, string? replyToMessageId = null, CancellationToken cancellationToken = default)` | Igual que `SendStickerAsync`, referenciando un sticker ya subido por `media_id`. |
| `string? VerifyWebhookChallenge(string mode, string verifyToken, string challenge)` | Resuelve el handshake `GET` de verificación del webhook contra `ArjuyWhatsAppOptions.VerifyToken`. |
| `Task<MResult<bool>> ProcessWebhookAsync(string rawBody, string? signatureHeader, CancellationToken cancellationToken = default)` | Valida la firma HMAC del `POST` entrante, parsea el payload de Meta, y entrega cada mensaje vía el evento `MessageReceived`/`IWhatsAppMessageHandler` y cada actualización de estado de entrega vía `MessageStatusUpdated`/`IWhatsAppStatusHandler`. |
| `event EventHandler<WhatsAppMessageReceivedEventArgs>? MessageReceived` | Se dispara por cada mensaje entrante procesado por `ProcessWebhookAsync`. |
| `event EventHandler<WhatsAppMessageStatusUpdateEventArgs>? MessageStatusUpdated` | Se dispara por cada actualización de estado de entrega (`sent`/`delivered`/`read`/`failed`) de un mensaje saliente procesada por `ProcessWebhookAsync`. |

Todos los métodos que devuelven `Task<MResult<T>>` usan el mismo tipo de resultado propio y
autocontenido (`IsSuccess`, `Data`, `Message`), sin excepciones para el flujo normal de error. Las
excepciones de red/HTTP se capturan internamente y se traducen a `MResult.Fail(...)`. Ver
`MANUAL.md`, sección 3, para el flujo completo de recepción de mensajes vía webhook.

**Responder citando un mensaje (`context.message_id`)**: todos los métodos de envío de contenido
(`SendTextAsync`, ambos overloads de `SendTemplateAsync`, `SendImageAsync`, `SendDocumentAsync`,
`SendImageByMediaIdAsync`, `SendDocumentByMediaIdAsync`, `SendAudioAsync`/`SendAudioByMediaIdAsync`,
`SendVideoAsync`/`SendVideoByMediaIdAsync`, `SendStickerAsync`/`SendStickerByMediaIdAsync`,
`SendLocationAsync`, `SendContactsAsync`, `SendInteractiveButtonsAsync`, `SendInteractiveListAsync`)
aceptan un parámetro opcional `replyToMessageId` al final de la firma (excepción: `SendReactionAsync`
no lo admite — la reacción ya referencia en sí misma al mensaje reaccionado).
Pasale el `wamid.` de un mensaje entrante (el mismo valor de `WhatsAppMessageReceived.MessageId`)
para que WhatsApp muestre el mensaje enviado como respuesta/cita de ese mensaje en el chat del
destinatario. Si se omite, el mensaje se envía sin contexto, como hasta ahora.

```csharp
await whatsAppClient.SendTextAsync("5491100000000", "¡Gracias por tu consulta!", replyToMessageId: mensajeEntrante.MessageId);
```

### Errores estructurados de Meta (`MResult.Error` / `MetaApiError`)

Cuando un `Fail` viene de una respuesta no exitosa de la Graph API (no de una validación local, como
los límites de botones/listas o falta de configuración), `MResult.Error` trae un `MetaApiError` con
el `code`, `error_subcode`, `type`, `fbtrace_id` y el body crudo que devolvió Meta — además del
`Message` de siempre, que sigue poblado para no romper a quien solo lo lee. Esto permite distinguir
programáticamente, por ejemplo, un número sin WhatsApp de un token vencido o de un rate limit, sin
tener que parsear el string de error a mano:

```csharp
var result = await whatsAppClient.SendTextAsync("5491100000000", "Hola");

if (!result.IsSuccess && result.Error is { IsRateLimited: true })
{
    // No hace falta manejarlo a mano en general — ver "Retry automático" más abajo, la librería
    // ya reintenta sola los errores transitorios. Este chequeo sirve para lógica adicional propia
    // (por ejemplo, encolar el mensaje para reintentar más tarde si se agotaron los reintentos).
}
```

`Error` es `null` cuando el resultado es exitoso o cuando el fallo es de validación local de la
librería (esos casos siguen usando `MResult.Fail(string)`, sin un error de Meta detrás).

## Retry automático con backoff exponencial

Ante un error transitorio de Meta (`MetaApiError.IsTransient` — rate limiting HTTP 429 o error 5xx
del lado de Meta), la librería reintenta automáticamente antes de devolver el `MResult.Fail`. Los
errores no transitorios (400, 401, 403, y las validaciones locales de botones/listas) **nunca** se
reintentan — no tiene sentido reintentar algo que va a fallar exactamente igual.

```json
{
  "ArjuyWhatsApp": {
    "MaxRetryAttempts": 3,
    "BaseRetryDelay": "00:00:00.500"
  }
}
```

- `MaxRetryAttempts` (default `3`): cantidad de reintentos, sin contar el intento inicial. `0`
  desactiva el retry por completo.
- `BaseRetryDelay` (default `500ms`): delay base del backoff exponencial — el reintento *N* espera
  `BaseRetryDelay * 2^(N-1)` con jitter aleatorio de ±20% (para no sincronizar reintentos entre
  varias instancias corriendo en paralelo). Con el default: ~500ms, ~1s, ~2s.
- Si la respuesta 429 de Meta trae el header `Retry-After`, ese valor se usa en vez del backoff
  calculado — Meta te está diciendo explícitamente cuánto esperar.

Aplica a todos los métodos que llaman a la Graph API (envío de mensajes, `UploadMediaAsync`,
`DownloadMediaAsync`). Agotados los reintentos, se devuelve el último `MResult.Fail` tal cual,
con el `MetaApiError` real de Meta.

## Normalización de números de teléfono

La normalización del número de destino (campo `"to"` del payload) es una estrategia inyectable,
`IPhoneNumberNormalizer`, no un método para heredar. La librería trae dos implementaciones
built-in y elige una automáticamente según `ArjuyWhatsAppOptions.CountryCode`:

- `"AR"` → `ArgentinaPhoneNumberNormalizer` — resuelve el "9" móvil de Argentina (va inmediatamente
  después del código de país `54`, no al final) y el "15" del marcado local de celular. Ver el XML
  doc de la clase para la limitación conocida sobre códigos de área de más de 2 dígitos.
- Cualquier otro valor (o sin configurar) → `DefaultPhoneNumberNormalizer` — solo recorta espacios,
  sin ninguna transformación adicional (comportamiento histórico de la librería).

```json
{
  "ArjuyWhatsApp": {
    "CountryCode": "AR"
  }
}
```

Si necesitás una regla propia (otro país, o una tabla de códigos de área más precisa que la
built-in de Argentina), registrá tu propia implementación en el contenedor de DI **antes** de
llamar a `AddArjuyWhatsApp` — la librería usa `TryAddSingleton`, así que tu registración gana y el
`CountryCode` de las opciones se ignora:

```csharp
services.AddSingleton<IPhoneNumberNormalizer, MiNormalizadorPropio>();
services.AddArjuyWhatsApp(builder.Configuration);
```

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

1. Audio/video/sticker con helpers dedicados por URL o `media_id` (hoy solo imagen/documento los
   tienen — para el resto de tipos hay que armar el payload genérico a mano).

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

---

Si esta librería te resultó útil, una ⭐ en el repo ayuda más de lo que parece.
