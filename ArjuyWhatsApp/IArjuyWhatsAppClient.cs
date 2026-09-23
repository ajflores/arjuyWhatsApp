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
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto. WhatsApp muestra el mensaje citado arriba de este en el chat del
    /// destinatario. Aplica al campo <c>context.message_id</c> del payload de envío.
    /// </param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendTextAsync(string phoneNumber, string message, string? replyToMessageId = null);

    /// <summary>
    /// Envía un mensaje basado en una plantilla previamente aprobada por Meta.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="templateName">Nombre de la plantilla aprobada.</param>
    /// <param name="languageCode">Código de idioma de la plantilla (ej. "es", "es_AR").</param>
    /// <param name="parameters">Parámetros posicionales del cuerpo de la plantilla, en orden.</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendTemplateAsync(string phoneNumber, string templateName, string languageCode, IEnumerable<string> parameters, string? replyToMessageId = null);

    /// <summary>
    /// Envía un mensaje basado en una plantilla previamente aprobada por Meta, con soporte además
    /// para header dinámico de media (imagen/video/documento) y/o botones dinámicos (URL o quick
    /// reply) — para el caso simple de una plantilla con solo body, preferí el overload de 4
    /// parámetros.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="templateName">Nombre de la plantilla aprobada.</param>
    /// <param name="languageCode">Código de idioma de la plantilla (ej. "es", "es_AR").</param>
    /// <param name="bodyParameters">Parámetros posicionales del cuerpo de la plantilla, en orden.</param>
    /// <param name="headerMedia">
    /// Media dinámica del header, si la plantilla fue aprobada con header de imagen/video/documento;
    /// <c>null</c> si la plantilla no tiene header o tiene header de texto fijo.
    /// </param>
    /// <param name="buttonParameters">
    /// Valores dinámicos para los botones de la plantilla que los necesiten (URL con placeholder, o
    /// quick reply); <c>null</c> u vacío si la plantilla no tiene botones dinámicos.
    /// </param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <returns>
    /// <c>MResult&lt;string&gt;.Fail(...)</c> si <paramref name="headerMedia"/> no tiene exactamente
    /// uno entre <see cref="WhatsAppTemplateHeaderMedia.Link"/> y <see cref="WhatsAppTemplateHeaderMedia.MediaId"/>
    /// seteado (sin llamar a Meta), o si Meta rechaza la request; <c>MResult&lt;string&gt;.Success(...)</c>
    /// con el message id si se envió.
    /// </returns>
    Task<MResult<string>> SendTemplateAsync(string phoneNumber, string templateName, string languageCode, IEnumerable<string> bodyParameters, WhatsAppTemplateHeaderMedia? headerMedia, IEnumerable<WhatsAppTemplateButtonParameter>? buttonParameters = null, string? replyToMessageId = null);

    /// <summary>
    /// Envía una imagen por URL pública, con caption opcional.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="imageUrl">URL pública de la imagen a enviar.</param>
    /// <param name="caption">Texto opcional que acompaña la imagen.</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendImageAsync(string phoneNumber, string imageUrl, string? caption = null, string? replyToMessageId = null);

    /// <summary>
    /// Envía un documento por URL pública, con nombre de archivo y caption opcional.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="documentUrl">URL pública del documento a enviar.</param>
    /// <param name="fileName">Nombre de archivo mostrado al destinatario.</param>
    /// <param name="caption">Texto opcional que acompaña el documento.</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendDocumentAsync(string phoneNumber, string documentUrl, string fileName, string? caption = null, string? replyToMessageId = null);

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
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendImageByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null, string? replyToMessageId = null);

    /// <summary>
    /// Envía un documento previamente subido con <see cref="UploadMediaAsync"/>, referenciándolo por
    /// su <c>media_id</c> en vez de por URL pública.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="mediaId">Media id devuelto por <see cref="UploadMediaAsync"/>.</param>
    /// <param name="fileName">Nombre de archivo opcional mostrado al destinatario.</param>
    /// <param name="caption">Texto opcional que acompaña el documento.</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendDocumentByMediaIdAsync(string phoneNumber, string mediaId, string? fileName = null, string? caption = null, string? replyToMessageId = null);

    /// <summary>
    /// Envía un audio (o nota de voz) por URL pública. Meta NO admite <c>caption</c> en mensajes de
    /// audio (a diferencia de imagen/documento/video) — no es una limitación de esta librería.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="audioUrl">URL pública del audio a enviar.</param>
    /// <param name="voice">
    /// <c>true</c> para que WhatsApp lo muestre como nota de voz (burbuja con forma de onda y
    /// reproductor inline) en vez de un archivo de audio genérico. Requiere OGG/Opus mono para
    /// reproducirse correctamente como nota de voz — ver la documentación de Meta sobre formatos
    /// soportados.
    /// </param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendAudioAsync(string phoneNumber, string audioUrl, bool voice = false, string? replyToMessageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía un audio previamente subido con <see cref="UploadMediaAsync"/>, referenciándolo por su
    /// <c>media_id</c> en vez de por URL pública. Ver <see cref="SendAudioAsync"/> para el resto de
    /// las restricciones (sin <c>caption</c>, formato para nota de voz).
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="mediaId">Media id devuelto por <see cref="UploadMediaAsync"/>.</param>
    /// <param name="voice">Ver <see cref="SendAudioAsync"/>.</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendAudioByMediaIdAsync(string phoneNumber, string mediaId, bool voice = false, string? replyToMessageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía un video por URL pública, con caption opcional (máximo 1024 caracteres, límite de Meta).
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="videoUrl">URL pública del video a enviar.</param>
    /// <param name="caption">Texto opcional que acompaña el video.</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendVideoAsync(string phoneNumber, string videoUrl, string? caption = null, string? replyToMessageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía un video previamente subido con <see cref="UploadMediaAsync"/>, referenciándolo por su
    /// <c>media_id</c> en vez de por URL pública.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="mediaId">Media id devuelto por <see cref="UploadMediaAsync"/>.</param>
    /// <param name="caption">Texto opcional que acompaña el video.</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendVideoByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null, string? replyToMessageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía un sticker por URL pública. Meta exige formato WebP (estático hasta 100KB, animado
    /// hasta 500KB) — esta librería no valida el archivo localmente, se deja que Meta rechace si no
    /// cumple. NO admite <c>caption</c> (no es una limitación de esta librería).
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="stickerUrl">URL pública del sticker (WebP) a enviar.</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendStickerAsync(string phoneNumber, string stickerUrl, string? replyToMessageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía un sticker previamente subido con <see cref="UploadMediaAsync"/>, referenciándolo por su
    /// <c>media_id</c> en vez de por URL pública. Ver <see cref="SendStickerAsync"/> para las
    /// restricciones de formato de Meta.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="mediaId">Media id devuelto por <see cref="UploadMediaAsync"/>.</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendStickerByMediaIdAsync(string phoneNumber, string mediaId, string? replyToMessageId = null, CancellationToken cancellationToken = default);

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
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <returns>
    /// <c>MResult&lt;string&gt;.Fail(...)</c> si se viola algún límite (sin llamar a Meta), o si Meta
    /// rechaza la request; <c>MResult&lt;string&gt;.Success(...)</c> con el message id si se envió.
    /// </returns>
    Task<MResult<string>> SendInteractiveButtonsAsync(string phoneNumber, string bodyText, IEnumerable<(string Id, string Title)> buttons, string? replyToMessageId = null);

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
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <returns>
    /// <c>MResult&lt;string&gt;.Fail(...)</c> si se viola algún límite (sin llamar a Meta), o si Meta
    /// rechaza la request; <c>MResult&lt;string&gt;.Success(...)</c> con el message id si se envió.
    /// </returns>
    Task<MResult<string>> SendInteractiveListAsync(string phoneNumber, string bodyText, string buttonText, IEnumerable<(string SectionTitle, IEnumerable<(string Id, string Title, string? Description)> Rows)> sections, string? replyToMessageId = null);

    /// <summary>
    /// Lista todas las plantillas de mensaje (de cualquier estado — aprobadas, pendientes,
    /// rechazadas, pausadas, deshabilitadas) de la cuenta de WhatsApp Business configurada en
    /// <see cref="ArjuyWhatsAppOptions.BusinessAccountId"/>, siguiendo automáticamente la
    /// paginación de la Graph API hasta agotar los resultados (hasta un máximo interno de páginas,
    /// para no loopear indefinidamente ante un comportamiento inesperado de la API). Pensado para
    /// que el consumidor pueda armar una UI de selección de plantillas, o validar en código el
    /// nombre/idioma exactos y la cantidad de parámetros que pide cada una antes de llamar a
    /// <see cref="SendTemplateAsync(string, string, string, IEnumerable{string}, string)"/>, sin tener que ir a copiarlos a mano del panel de Meta.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en cada request HTTP como en los delays de reintento.</param>
    /// <returns>
    /// Resultado con la lista completa de plantillas cuando la operación es exitosa;
    /// <c>MResult&lt;IReadOnlyList&lt;WhatsAppMessageTemplate&gt;&gt;.Fail(...)</c> si falta
    /// configurar <see cref="ArjuyWhatsAppOptions.BusinessAccountId"/>, o si Meta rechaza la
    /// request (sin datos parciales — es todo o nada).
    /// </returns>
    Task<MResult<IReadOnlyList<WhatsAppMessageTemplate>>> GetMessageTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un mensaje entrante como leído (doble tilde azul para el destinatario). Meta solo
    /// permite marcar como leído un mensaje recibido dentro de los últimos 30 días — pasado ese
    /// plazo, la request falla. Marcar un mensaje como leído también marca como leídos todos los
    /// mensajes anteriores de la misma conversación.
    /// </summary>
    /// <param name="messageId">Id del mensaje entrante a marcar como leído (el mismo que llega en <see cref="WhatsAppMessageReceived.MessageId"/>).</param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns><c>MResult&lt;bool&gt;.Fail(...)</c> si falta configuración o Meta rechaza la request; <c>MResult&lt;bool&gt;.Success(true)</c> si se marcó correctamente.</returns>
    Task<MResult<bool>> MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual que <see cref="MarkAsReadAsync"/>, pero además le muestra al destinatario el indicador
    /// de "escribiendo..." en el chat. Meta lo descarta automáticamente en cuanto se envía una
    /// respuesta, o a los 25 segundos si no se envía ninguna — lo que ocurra primero. Solo tiene
    /// sentido usarlo si efectivamente se va a responder a continuación; mostrarlo sin responder
    /// después es una mala experiencia para el usuario.
    /// </summary>
    /// <param name="messageId">Id del mensaje entrante a marcar como leído (el mismo que llega en <see cref="WhatsAppMessageReceived.MessageId"/>).</param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns><c>MResult&lt;bool&gt;.Fail(...)</c> si falta configuración o Meta rechaza la request; <c>MResult&lt;bool&gt;.Success(true)</c> si se marcó correctamente y se activó el indicador.</returns>
    Task<MResult<bool>> MarkAsReadWithTypingIndicatorAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reacciona con un emoji a un mensaje entrante o saliente. Pasar <see cref="string.Empty"/>
    /// como <paramref name="emoji"/> remueve una reacción puesta anteriormente (mecanismo oficial de
    /// Meta para "unreact" — no es un error, Meta responde éxito igual). No admite
    /// <c>replyToMessageId</c>: la reacción ya es en sí misma una referencia a <paramref name="messageId"/>,
    /// no lleva <c>context</c> propio según la documentación de Meta.
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="messageId">Id (<c>wamid.</c>) del mensaje al que se reacciona.</param>
    /// <param name="emoji">Emoji de la reacción, o <see cref="string.Empty"/> para remover una reacción existente.</param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendReactionAsync(string phoneNumber, string messageId, string emoji, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía una ubicación (coordenadas, con nombre y dirección opcionales).
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="latitude">Latitud.</param>
    /// <param name="longitude">Longitud.</param>
    /// <param name="name">Nombre del lugar, ej. "Oficina Jujuy Dev" (opcional).</param>
    /// <param name="address">Dirección del lugar (opcional).</param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns>Resultado con el message id devuelto por Meta cuando la operación es exitosa.</returns>
    Task<MResult<string>> SendLocationAsync(string phoneNumber, double latitude, double longitude, string? name = null, string? address = null, string? replyToMessageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía una tarjeta de contacto (o varias en un mismo mensaje).
    /// </summary>
    /// <param name="phoneNumber">Número de destino en formato internacional (con o sin "+").</param>
    /// <param name="contacts">
    /// Uno o más contactos a enviar. Cada uno requiere <see cref="WhatsAppContactName.FormattedName"/>
    /// no vacío — se valida localmente antes de llamar a Meta.
    /// </param>
    /// <param name="replyToMessageId">
    /// Id (<c>wamid.</c>) de un mensaje entrante al que este envío responde/cita, o <c>null</c> para
    /// un mensaje sin contexto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación, respetado tanto en la request HTTP como en los delays de reintento.</param>
    /// <returns>
    /// <c>MResult&lt;string&gt;.Fail(...)</c> si <paramref name="contacts"/> está vacío o algún
    /// contacto no tiene <see cref="WhatsAppContactName.FormattedName"/> (sin llamar a Meta), o si
    /// Meta rechaza la request; <c>MResult&lt;string&gt;.Success(...)</c> con el message id si se envió.
    /// </returns>
    Task<MResult<string>> SendContactsAsync(string phoneNumber, IEnumerable<WhatsAppContact> contacts, string? replyToMessageId = null, CancellationToken cancellationToken = default);
}
