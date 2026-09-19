namespace ArjuyWhatsApp;

/// <summary>
/// Cliente para interactuar con la API oficial de WhatsApp Cloud API de Meta:
/// envío de mensajes de texto, plantillas, imágenes y documentos, descarga de media, y
/// recepción de mensajes entrantes vía webhook (verify challenge + validación de firma +
/// parseo del payload).
/// </summary>
public interface IArjuyWhatsAppClient
{
    /// <summary>
    /// Se dispara por cada mensaje entrante que <see cref="ProcessWebhookAsync"/> logra parsear
    /// desde un webhook válido de Meta. Pensado para suscripciones livianas/ad-hoc; para lógica de
    /// negocio que necesite resolver otros servicios <c>Scoped</c> (por ejemplo un
    /// <c>DbContext</c>), preferí registrar un <see cref="IWhatsAppMessageHandler"/> por DI —
    /// ambos mecanismos conviven y se invocan para el mismo mensaje.
    /// </summary>
    event EventHandler<WhatsAppMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Se dispara por cada actualización de estado de entrega (sent/delivered/read/failed) de un
    /// mensaje saliente que <see cref="ProcessWebhookAsync"/> logra parsear desde un webhook válido
    /// de Meta. Pensado para suscripciones livianas/ad-hoc; para lógica de negocio que necesite
    /// resolver otros servicios <c>Scoped</c> (por ejemplo un <c>DbContext</c>), preferí registrar
    /// un <see cref="IWhatsAppStatusHandler"/> por DI — ambos mecanismos conviven y se invocan para
    /// el mismo estado.
    /// </summary>
    event EventHandler<WhatsAppMessageStatusUpdateEventArgs>? MessageStatusUpdated;

    /// <summary>
    /// Resuelve el handshake de verificación (<c>GET</c>) que Meta hace contra la Callback URL del
    /// webhook al configurarla o re-verificarla en el panel de Meta for Developers.
    /// </summary>
    /// <param name="mode">Valor del query param <c>hub.mode</c> recibido de Meta.</param>
    /// <param name="verifyToken">Valor del query param <c>hub.verify_token</c> recibido de Meta, a comparar contra <see cref="ArjuyWhatsAppOptions.VerifyToken"/>.</param>
    /// <param name="challenge">Valor del query param <c>hub.challenge</c> recibido de Meta.</param>
    /// <returns><paramref name="challenge"/> tal cual, si <paramref name="mode"/> es <c>"subscribe"</c> y <paramref name="verifyToken"/> coincide con el configurado; <c>null</c> en caso contrario (el consumidor debe responder con un status distinto de 200, por ejemplo <c>403 Forbidden</c>).</returns>
    string? VerifyWebhookChallenge(string mode, string verifyToken, string challenge);

    /// <summary>
    /// Procesa un <c>POST</c> entrante del webhook de WhatsApp: valida la firma HMAC-SHA256 del
    /// header <c>X-Hub-Signature-256</c> contra <see cref="ArjuyWhatsAppOptions.AppSecret"/> usando
    /// el body crudo, parsea el payload de Meta, y por cada mensaje entrante dispara
    /// <see cref="MessageReceived"/> e invoca a cada <see cref="IWhatsAppMessageHandler"/>
    /// registrado por DI.
    /// </summary>
    /// <param name="rawBody">Body crudo exacto de la request (los mismos bytes que llegaron, decodificados como UTF-8, sin pasar por ninguna deserialización previa) — necesario para que el HMAC calculado coincida con el de Meta.</param>
    /// <param name="signatureHeader">Valor del header <c>X-Hub-Signature-256</c> (formato <c>"sha256=&lt;hex&gt;"</c>), o <c>null</c> si no vino.</param>
    /// <param name="cancellationToken">Token de cancelación de la request.</param>
    /// <returns>
    /// <c>MResult&lt;bool&gt;.Fail(...)</c> si la firma no es válida (no se procesa nada en ese
    /// caso) o si el payload no se pudo parsear; <c>MResult&lt;bool&gt;.Success(true)</c> si se
    /// validó y procesó correctamente (incluso si el payload no traía mensajes ni estados, algo
    /// que no debería pasar en la práctica pero que la librería tolera igual).
    /// </returns>
    Task<MResult<bool>> ProcessWebhookAsync(string rawBody, string? signatureHeader, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía un mensaje de texto libre a un número de WhatsApp.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="message">Contenido del mensaje de texto.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendTextAsync(string phoneNumber, string message);

    /// <summary>
    /// Envía un mensaje basado en una plantilla previamente aprobada por Meta.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="templateName">Nombre de la plantilla aprobada.</param>
    /// <param name="languageCode">Código de idioma de la plantilla (ej. "es", "es_AR").</param>
    /// <param name="parameters">Parámetros posicionales del cuerpo de la plantilla, en orden.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendTemplateAsync(string phoneNumber, string templateName, string languageCode, IEnumerable<string> parameters);

    /// <summary>
    /// Envía una imagen por URL pública, con caption opcional.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="imageUrl">URL pública de la imagen a enviar.</param>
    /// <param name="caption">Texto opcional que acompaña la imagen.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendImageAsync(string phoneNumber, string imageUrl, string? caption = null);

    /// <summary>
    /// Envía un documento por URL pública, con nombre de archivo y caption opcional.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="documentUrl">URL pública del documento a enviar.</param>
    /// <param name="fileName">Nombre de archivo mostrado al destinatario.</param>
    /// <param name="caption">Texto opcional que acompaña el documento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendDocumentAsync(string phoneNumber, string documentUrl, string fileName, string? caption = null);

    /// <summary>
    /// Descarga el contenido binario de un archivo de media a partir de su media id (recibido, por ejemplo, en un webhook entrante).
    /// </summary>
    /// <param name="mediaId">Identificador de media devuelto por Meta.</param>
    /// <returns>Resultado con los bytes del archivo descargado cuando la operación es exitosa.</returns>
    Task<MResult<byte[]>> DownloadMediaAsync(string mediaId);

    /// <summary>
    /// Sube un archivo local a los servidores de Meta (<c>POST /{phone-number-id}/media</c>, sin
    /// necesidad de exponerlo antes en una URL pública) y devuelve el <c>media_id</c> resultante,
    /// utilizable luego en <see cref="SendImageByMediaIdAsync"/> o <see cref="SendDocumentByMediaIdAsync"/>.
    /// Pensado para archivos privados (por ejemplo, un PDF de una reserva generado en el momento)
    /// que el consumidor de la librería no quiere o no puede publicar en una URL accesible por Meta.
    /// </summary>
    /// <param name="fileContent">Contenido binario del archivo a subir.</param>
    /// <param name="fileName">Nombre de archivo (solo se usa para el multipart; no queda asociado al media id en Meta).</param>
    /// <param name="mimeType">Tipo MIME del archivo (ej. <c>"application/pdf"</c>, <c>"image/jpeg"</c>), enviado como campo <c>type</c> del multipart.</param>
    /// <returns>Resultado con el <c>media_id</c> devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> UploadMediaAsync(byte[] fileContent, string fileName, string mimeType);

    /// <summary>
    /// Envía una imagen previamente subida con <see cref="UploadMediaAsync"/>, referenciándola por
    /// su <c>media_id</c> en vez de por URL pública.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="mediaId">Media id devuelto por <see cref="UploadMediaAsync"/>.</param>
    /// <param name="caption">Texto opcional que acompaña la imagen.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendImageByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null);

    /// <summary>
    /// Envía un documento previamente subido con <see cref="UploadMediaAsync"/>, referenciándolo por
    /// su <c>media_id</c> en vez de por URL pública.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="mediaId">Media id devuelto por <see cref="UploadMediaAsync"/>.</param>
    /// <param name="fileName">Nombre de archivo opcional mostrado al destinatario.</param>
    /// <param name="caption">Texto opcional que acompaña el documento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendDocumentByMediaIdAsync(string phoneNumber, string mediaId, string? fileName = null, string? caption = null);

    /// <summary>
    /// Envía un mensaje interactivo con hasta 3 botones de respuesta rápida (<c>interactive.type = "button"</c>).
    /// Pensado para menús simples de 2-3 opciones (ej. "Confirmar" / "Cancelar" / "Hablar con un asesor");
    /// para más opciones o cuando cada opción necesita una descripción, usar <see cref="SendInteractiveListAsync"/>.
    /// Cuando el destinatario toca un botón, Meta lo entrega como un mensaje entrante con
    /// <see cref="WhatsAppMessageReceived.Type"/> igual a <see cref="WhatsAppMessageType.Interactive"/> y el
    /// <c>Id</c> del botón elegido en <see cref="WhatsAppMessageReceived.InteractiveReplyId"/>.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="bodyText">Texto principal del mensaje (máximo 1024 caracteres, límite de Meta).</param>
    /// <param name="buttons">
    /// Botones a mostrar, en orden. Límites de Meta validados antes de llamar a la API: máximo 3
    /// botones, <c>Id</c> hasta 256 caracteres, <c>Title</c> hasta 20 caracteres y único entre los
    /// botones del mensaje.
    /// </param>
    /// <returns>
    /// <c>MResult&lt;string&gt;.Fail(...)</c> si se viola algún límite (sin llamar a Meta), o si Meta
    /// rechaza la request; <c>MResult&lt;string&gt;.Success(...)</c> con el message id si se envió.
    /// </returns>
    Task<MResult<string>> SendInteractiveButtonsAsync(string phoneNumber, string bodyText, IEnumerable<(string Id, string Title)> buttons);

    /// <summary>
    /// Envía un mensaje interactivo con un menú desplegable de secciones y filas (<c>interactive.type = "list"</c>).
    /// Pensado para más de 3 opciones, o cuando cada opción se beneficia de una descripción corta
    /// adicional; para 2-3 opciones simples, preferir <see cref="SendInteractiveButtonsAsync"/> (una
    /// interacción menos para el usuario: no necesita abrir el menú). Cuando el destinatario elige una
    /// fila, Meta lo entrega como un mensaje entrante con <see cref="WhatsAppMessageReceived.Type"/>
    /// igual a <see cref="WhatsAppMessageType.Interactive"/> y el <c>Id</c> de la fila elegida en
    /// <see cref="WhatsAppMessageReceived.InteractiveReplyId"/>.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="bodyText">Texto principal del mensaje (máximo 4096 caracteres, límite de Meta).</param>
    /// <param name="buttonText">Texto del botón que abre el menú desplegable (máximo 20 caracteres, límite de Meta).</param>
    /// <param name="sections">
    /// Secciones con sus filas, en orden. Límites de Meta validados antes de llamar a la API: máximo
    /// 10 secciones, máximo 10 filas en total sumando todas las secciones, título de sección hasta 24
    /// caracteres, <c>Id</c> de fila hasta 200 caracteres, <c>Title</c> de fila hasta 24 caracteres y
    /// <c>Description</c> de fila (opcional) hasta 72 caracteres.
    /// </param>
    /// <returns>
    /// <c>MResult&lt;string&gt;.Fail(...)</c> si se viola algún límite (sin llamar a Meta), o si Meta
    /// rechaza la request; <c>MResult&lt;string&gt;.Success(...)</c> con el message id si se envió.
    /// </returns>
    Task<MResult<string>> SendInteractiveListAsync(string phoneNumber, string bodyText, string buttonText, IEnumerable<(string SectionTitle, IEnumerable<(string Id, string Title, string? Description)> Rows)> sections);
}
