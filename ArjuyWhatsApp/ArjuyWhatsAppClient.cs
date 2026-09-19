using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArjuyWhatsApp;

/// <summary>
/// Implementación de <see cref="IArjuyWhatsAppClient"/> que se comunica directamente con la
/// Graph API de Meta (WhatsApp Cloud API) usando un <see cref="HttpClient"/> nombrado
/// obtenido de <see cref="IHttpClientFactory"/>. No depende de ningún paquete de terceros.
/// </summary>
public class ArjuyWhatsAppClient : IArjuyWhatsAppClient
{
    /// <summary>Nombre del <see cref="HttpClient"/> registrado para este cliente vía <see cref="IHttpClientFactory"/>.</summary>
    public const string HttpClientName = "ArjuyWhatsApp";

    // -----------------------------------------------------------------------
    // Límites de mensajes interactivos, confirmados contra la documentación oficial de Meta
    // (WhatsApp Cloud API, "Interactive Reply Buttons Messages" e "Interactive List Messages",
    // developers.facebook.com/docs/whatsapp/cloud-api/messages/) al 2026-09-17. Se validan acá,
    // ANTES de llamar a Meta, para fallar rápido y claro en vez de dejar que la Graph API
    // devuelva un 400 críptico.
    // -----------------------------------------------------------------------

    private const int MaxInteractiveButtons = 3;
    private const int MaxButtonIdLength = 256;
    private const int MaxButtonTitleLength = 20;

    private const int MaxListButtonTextLength = 20;
    private const int MaxListSections = 10;
    private const int MaxListRowsTotal = 10;
    private const int MaxListSectionTitleLength = 24;
    private const int MaxListRowIdLength = 200;
    private const int MaxListRowTitleLength = 24;
    private const int MaxListRowDescriptionLength = 72;

    private static readonly JsonSerializerOptions _webhookJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ArjuyWhatsAppOptions _options;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ArjuyWhatsAppClient> _logger;

    /// <inheritdoc />
    public event EventHandler<WhatsAppMessageReceivedEventArgs>? MessageReceived;

    /// <inheritdoc />
    public event EventHandler<WhatsAppMessageStatusUpdateEventArgs>? MessageStatusUpdated;

    /// <summary>Crea una nueva instancia del cliente de WhatsApp Cloud API.</summary>
    /// <param name="httpClientFactory">Fábrica de <see cref="HttpClient"/> usada para obtener el cliente nombrado <see cref="HttpClientName"/>.</param>
    /// <param name="options">Opciones de configuración (token, phone number id, versión de API, etc.).</param>
    /// <param name="serviceScopeFactory">
    /// Fábrica usada para crear un scope de DI por cada mensaje entrante y así poder resolver
    /// <see cref="IWhatsAppMessageHandler"/> registrados como <c>Scoped</c> aunque este cliente se
    /// registre como <c>Singleton</c> (ver <c>ServiceCollectionExtensions</c> para el porqué).
    /// </param>
    /// <param name="logger">Logger usado para reportar errores de handlers individuales sin interrumpir el procesamiento del webhook.</param>
    public ArjuyWhatsAppClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ArjuyWhatsAppOptions> options,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ArjuyWhatsAppClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string? VerifyWebhookChallenge(string mode, string verifyToken, string challenge)
    {
        if (mode == "subscribe" && !string.IsNullOrEmpty(_options.VerifyToken) && verifyToken == _options.VerifyToken)
        {
            return challenge;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<MResult<bool>> ProcessWebhookAsync(string rawBody, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        if (!IsValidSignature(rawBody, signatureHeader, _options.AppSecret))
        {
            _logger.LogWarning("ArjuyWhatsApp: firma de webhook inválida — posible request no autorizado");
            return MResult<bool>.Fail("Firma de webhook inválida (X-Hub-Signature-256 no coincide).");
        }

        WhatsAppWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(rawBody, _webhookJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "ArjuyWhatsApp: no se pudo parsear el payload del webhook");
            return MResult<bool>.Fail($"No se pudo parsear el payload del webhook: {ex.Message}");
        }

        if (payload?.Entry == null)
        {
            return MResult<bool>.Success(true, "Payload sin entries — nada que procesar.");
        }

        foreach (var entry in payload.Entry)
        {
            foreach (var change in entry.Changes)
            {
                if (change.Value == null)
                {
                    continue;
                }

                if (change.Value.Messages != null)
                {
                    foreach (var inboundMessage in change.Value.Messages)
                    {
                        var message = MapToMessageReceived(inboundMessage);
                        await DispatchAsync(message, cancellationToken);
                    }
                }

                if (change.Value.Statuses != null)
                {
                    foreach (var status in change.Value.Statuses)
                    {
                        var statusUpdate = MapToStatusUpdate(status);
                        await DispatchAsync(statusUpdate, cancellationToken);
                    }
                }
            }
        }

        return MResult<bool>.Success(true);
    }

    private async Task DispatchAsync(WhatsAppMessageReceived message, CancellationToken cancellationToken)
    {
        try
        {
            MessageReceived?.Invoke(this, new WhatsAppMessageReceivedEventArgs(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArjuyWhatsApp: un suscriptor de MessageReceived lanzó una excepción para el mensaje {MessageId}", message.MessageId);
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IWhatsAppMessageHandler>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ArjuyWhatsApp: {Handler} lanzó una excepción procesando el mensaje {MessageId} — se continúa con los demás handlers", handler.GetType().Name, message.MessageId);
            }
        }
    }

    private async Task DispatchAsync(WhatsAppMessageStatusUpdate statusUpdate, CancellationToken cancellationToken)
    {
        try
        {
            MessageStatusUpdated?.Invoke(this, new WhatsAppMessageStatusUpdateEventArgs(statusUpdate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArjuyWhatsApp: un suscriptor de MessageStatusUpdated lanzó una excepción para el mensaje {MessageId}", statusUpdate.MessageId);
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IWhatsAppStatusHandler>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(statusUpdate, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ArjuyWhatsApp: {Handler} lanzó una excepción procesando el estado del mensaje {MessageId} — se continúa con los demás handlers", handler.GetType().Name, statusUpdate.MessageId);
            }
        }
    }

    private static WhatsAppMessageReceived MapToMessageReceived(WhatsAppWebhookInboundMessage inboundMessage)
    {
        var type = inboundMessage.Type switch
        {
            "text" => WhatsAppMessageType.Text,
            "image" => WhatsAppMessageType.Image,
            "document" => WhatsAppMessageType.Document,
            "interactive" => WhatsAppMessageType.Interactive,
            _ => WhatsAppMessageType.Unknown
        };

        var interactiveReply = type == WhatsAppMessageType.Interactive
            ? inboundMessage.Interactive?.ButtonReply ?? inboundMessage.Interactive?.ListReply
            : null;

        return new WhatsAppMessageReceived
        {
            From = inboundMessage.From,
            MessageId = inboundMessage.Id,
            Type = type,
            Text = type == WhatsAppMessageType.Text ? inboundMessage.Text?.Body : null,
            MediaId = type switch
            {
                WhatsAppMessageType.Image => inboundMessage.Image?.Id,
                WhatsAppMessageType.Document => inboundMessage.Document?.Id,
                _ => null
            },
            InteractiveReplyId = interactiveReply?.Id,
            InteractiveReplyTitle = interactiveReply?.Title,
            Timestamp = ParseTimestamp(inboundMessage.Timestamp)
        };
    }

    private static WhatsAppMessageStatusUpdate MapToStatusUpdate(WhatsAppWebhookStatus status)
    {
        var mappedStatus = status.Status switch
        {
            "sent" => WhatsAppMessageStatus.Sent,
            "delivered" => WhatsAppMessageStatus.Delivered,
            "read" => WhatsAppMessageStatus.Read,
            "failed" => WhatsAppMessageStatus.Failed,
            _ => WhatsAppMessageStatus.Sent
        };

        var firstError = mappedStatus == WhatsAppMessageStatus.Failed
            ? status.Errors.FirstOrDefault()
            : null;

        return new WhatsAppMessageStatusUpdate
        {
            MessageId = status.Id,
            RecipientPhoneNumber = status.RecipientId,
            Status = mappedStatus,
            Timestamp = ParseTimestamp(status.Timestamp),
            ErrorCode = firstError?.Code,
            ErrorMessage = firstError?.Message
        };
    }

    private static DateTime ParseTimestamp(string timestamp)
    {
        if (long.TryParse(timestamp, out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }

        return default;
    }

    private static bool IsValidSignature(string rawBody, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] receivedHash;
        try
        {
            receivedHash = Convert.FromHexString(signatureHeader[prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));

        return CryptographicOperations.FixedTimeEquals(computedHash, receivedHash);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendTextAsync(string phoneNumber, string message)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhoneNumber(phoneNumber),
            type = "text",
            text = new { body = message }
        };

        return await SendMessagePayloadAsync(payload);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendTemplateAsync(string phoneNumber, string templateName, string languageCode, IEnumerable<string> parameters)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhoneNumber(phoneNumber),
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components = new object[]
                {
                    new
                    {
                        type = "body",
                        parameters = parameters.Select(p => new { type = "text", text = p }).ToArray()
                    }
                }
            }
        };

        return await SendMessagePayloadAsync(payload);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendImageAsync(string phoneNumber, string imageUrl, string? caption = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhoneNumber(phoneNumber),
            type = "image",
            image = new { link = imageUrl, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendDocumentAsync(string phoneNumber, string documentUrl, string fileName, string? caption = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhoneNumber(phoneNumber),
            type = "document",
            document = new { link = documentUrl, filename = fileName, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload);
    }

    /// <inheritdoc />
    public async Task<MResult<byte[]>> DownloadMediaAsync(string mediaId)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            return MResult<byte[]>.Fail("ArjuyWhatsApp no está configurado: falta AccessToken.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            // Paso 1: obtener la URL de descarga temporal desde Meta.
            var metaUrl = $"https://graph.facebook.com/{_options.ApiVersion}/{mediaId}";
            using var metaRequest = new HttpRequestMessage(HttpMethod.Get, metaUrl);
            metaRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

            using var metaResponse = await client.SendAsync(metaRequest);
            var metaBody = await metaResponse.Content.ReadAsStringAsync();

            if (!metaResponse.IsSuccessStatusCode)
            {
                return MResult<byte[]>.Fail($"Error Meta API ({(int)metaResponse.StatusCode}): {metaBody}");
            }

            using var metaDoc = JsonDocument.Parse(metaBody);
            if (!metaDoc.RootElement.TryGetProperty("url", out var urlProp))
            {
                return MResult<byte[]>.Fail("Meta no devolvió URL de descarga para el media id indicado.");
            }

            var downloadUrl = urlProp.GetString();
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return MResult<byte[]>.Fail("Meta devolvió una URL de descarga vacía.");
            }

            // Paso 2: descargar el archivo binario usando el mismo token.
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            downloadRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

            using var downloadResponse = await client.SendAsync(downloadRequest);
            if (!downloadResponse.IsSuccessStatusCode)
            {
                var errorBody = await downloadResponse.Content.ReadAsStringAsync();
                return MResult<byte[]>.Fail($"No se pudo descargar el archivo de Meta ({(int)downloadResponse.StatusCode}): {errorBody}");
            }

            var data = await downloadResponse.Content.ReadAsByteArrayAsync();
            return MResult<byte[]>.Success(data);
        }
        catch (Exception ex)
        {
            return MResult<byte[]>.Fail(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<MResult<string>> UploadMediaAsync(byte[] fileContent, string fileName, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            return MResult<string>.Fail("ArjuyWhatsApp no está configurado: falta AccessToken o PhoneNumberId.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.PhoneNumberId}/media";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

            using var content = new MultipartFormDataContent();
            using var fileStreamContent = new ByteArrayContent(fileContent);
            fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
            content.Add(fileStreamContent, "file", fileName);
            content.Add(new StringContent(mimeType), "type");
            content.Add(new StringContent("whatsapp"), "messaging_product");
            request.Content = content;

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return MResult<string>.Fail($"Error Meta API ({(int)response.StatusCode}): {body}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("id", out var idProp))
            {
                return MResult<string>.Fail("Meta no devolvió un media id para el archivo subido.");
            }

            return MResult<string>.Success(idProp.GetString() ?? string.Empty);
        }
        catch (Exception ex)
        {
            return MResult<string>.Fail(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendImageByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhoneNumber(phoneNumber),
            type = "image",
            image = new { id = mediaId, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendDocumentByMediaIdAsync(string phoneNumber, string mediaId, string? fileName = null, string? caption = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhoneNumber(phoneNumber),
            type = "document",
            document = new { id = mediaId, filename = fileName ?? string.Empty, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendInteractiveButtonsAsync(string phoneNumber, string bodyText, IEnumerable<(string Id, string Title)> buttons)
    {
        var buttonList = buttons.ToList();

        if (buttonList.Count == 0)
        {
            return MResult<string>.Fail("SendInteractiveButtonsAsync requiere al menos un botón.");
        }

        if (buttonList.Count > MaxInteractiveButtons)
        {
            return MResult<string>.Fail($"SendInteractiveButtonsAsync admite como máximo {MaxInteractiveButtons} botones (Meta rechaza más) — se recibieron {buttonList.Count}. Para más opciones, usar SendInteractiveListAsync.");
        }

        foreach (var button in buttonList)
        {
            if (string.IsNullOrWhiteSpace(button.Id))
            {
                return MResult<string>.Fail("Cada botón requiere un Id no vacío.");
            }

            if (button.Id.Length > MaxButtonIdLength)
            {
                return MResult<string>.Fail($"El Id del botón '{button.Id}' supera el máximo de {MaxButtonIdLength} caracteres que admite Meta.");
            }

            if (string.IsNullOrWhiteSpace(button.Title))
            {
                return MResult<string>.Fail("Cada botón requiere un Title no vacío.");
            }

            if (button.Title.Length > MaxButtonTitleLength)
            {
                return MResult<string>.Fail($"El título del botón '{button.Title}' supera el máximo de {MaxButtonTitleLength} caracteres que admite Meta.");
            }
        }

        var duplicateTitles = buttonList
            .GroupBy(b => b.Title, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateTitles.Count > 0)
        {
            return MResult<string>.Fail($"Meta requiere que los títulos de los botones sean únicos — título repetido: '{duplicateTitles[0]}'.");
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhoneNumber(phoneNumber),
            type = "interactive",
            interactive = new
            {
                type = "button",
                body = new { text = bodyText },
                action = new
                {
                    buttons = buttonList.Select(b => new
                    {
                        type = "reply",
                        reply = new { id = b.Id, title = b.Title }
                    }).ToArray()
                }
            }
        };

        return await SendMessagePayloadAsync(payload);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendInteractiveListAsync(string phoneNumber, string bodyText, string buttonText, IEnumerable<(string SectionTitle, IEnumerable<(string Id, string Title, string? Description)> Rows)> sections)
    {
        if (string.IsNullOrWhiteSpace(buttonText))
        {
            return MResult<string>.Fail("SendInteractiveListAsync requiere un buttonText no vacío.");
        }

        if (buttonText.Length > MaxListButtonTextLength)
        {
            return MResult<string>.Fail($"El buttonText '{buttonText}' supera el máximo de {MaxListButtonTextLength} caracteres que admite Meta.");
        }

        var sectionList = sections
            .Select(s => (s.SectionTitle, Rows: s.Rows.ToList()))
            .ToList();

        if (sectionList.Count == 0)
        {
            return MResult<string>.Fail("SendInteractiveListAsync requiere al menos una sección.");
        }

        if (sectionList.Count > MaxListSections)
        {
            return MResult<string>.Fail($"SendInteractiveListAsync admite como máximo {MaxListSections} secciones (Meta rechaza más) — se recibieron {sectionList.Count}.");
        }

        var totalRows = sectionList.Sum(s => s.Rows.Count);
        if (totalRows == 0)
        {
            return MResult<string>.Fail("SendInteractiveListAsync requiere al menos una fila en alguna sección.");
        }

        if (totalRows > MaxListRowsTotal)
        {
            return MResult<string>.Fail($"SendInteractiveListAsync admite como máximo {MaxListRowsTotal} filas en total sumando todas las secciones (Meta rechaza más) — se recibieron {totalRows}.");
        }

        var allRowIds = new List<string>();

        foreach (var section in sectionList)
        {
            if (string.IsNullOrWhiteSpace(section.SectionTitle))
            {
                return MResult<string>.Fail("Cada sección requiere un SectionTitle no vacío.");
            }

            if (section.SectionTitle.Length > MaxListSectionTitleLength)
            {
                return MResult<string>.Fail($"El título de sección '{section.SectionTitle}' supera el máximo de {MaxListSectionTitleLength} caracteres que admite Meta.");
            }

            foreach (var row in section.Rows)
            {
                if (string.IsNullOrWhiteSpace(row.Id))
                {
                    return MResult<string>.Fail("Cada fila requiere un Id no vacío.");
                }

                if (row.Id.Length > MaxListRowIdLength)
                {
                    return MResult<string>.Fail($"El Id de la fila '{row.Id}' supera el máximo de {MaxListRowIdLength} caracteres que admite Meta.");
                }

                if (string.IsNullOrWhiteSpace(row.Title))
                {
                    return MResult<string>.Fail("Cada fila requiere un Title no vacío.");
                }

                if (row.Title.Length > MaxListRowTitleLength)
                {
                    return MResult<string>.Fail($"El título de la fila '{row.Title}' supera el máximo de {MaxListRowTitleLength} caracteres que admite Meta.");
                }

                if (row.Description is { Length: > MaxListRowDescriptionLength })
                {
                    return MResult<string>.Fail($"La descripción de la fila '{row.Title}' supera el máximo de {MaxListRowDescriptionLength} caracteres que admite Meta.");
                }

                allRowIds.Add(row.Id);
            }
        }

        var duplicateRowIds = allRowIds
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateRowIds.Count > 0)
        {
            return MResult<string>.Fail($"Los Id de fila deben ser únicos en todo el mensaje — id repetido: '{duplicateRowIds[0]}'.");
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhoneNumber(phoneNumber),
            type = "interactive",
            interactive = new
            {
                type = "list",
                body = new { text = bodyText },
                action = new
                {
                    button = buttonText,
                    sections = sectionList.Select(s => new
                    {
                        title = s.SectionTitle,
                        rows = s.Rows.Select(r => new
                        {
                            id = r.Id,
                            title = r.Title,
                            description = r.Description ?? string.Empty
                        }).ToArray()
                    }).ToArray()
                }
            }
        };

        return await SendMessagePayloadAsync(payload);
    }

    /// <summary>
    /// Hook de normalización del número de destino antes de enviarlo en el campo "to" del payload.
    /// La implementación por defecto no hace ninguna transformación (solo recorta espacios en blanco);
    /// las aplicaciones que necesiten reglas específicas de un país o formato (por ejemplo, el "9"
    /// móvil de Argentina) deben heredar de esta clase y sobreescribir este método. Ver README, sección Roadmap.
    /// </summary>
    /// <param name="phoneNumber">Número de teléfono tal como lo recibe la aplicación consumidora.</param>
    /// <returns>Número normalizado a enviar en el payload de la API de Meta.</returns>
    protected virtual string NormalizePhoneNumber(string phoneNumber)
    {
        return phoneNumber.Trim();
    }

    // -----------------------------------------------------------------------

    private async Task<MResult<string>> SendMessagePayloadAsync(object payload)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            return MResult<string>.Fail("ArjuyWhatsApp no está configurado: falta AccessToken o PhoneNumberId.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.PhoneNumberId}/messages";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);
            request.Content = JsonContent.Create(payload);

            // TODO: Meta aplica rate-limiting sobre este endpoint (respuesta 429 con header Retry-After).
            // Esta versión no implementa retry/backoff automático — queda pendiente para una futura iteración
            // (ver README, sección Roadmap / Pendiente).
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return MResult<string>.Fail($"Error Meta API ({(int)response.StatusCode}): {body}");
            }

            var messageId = ExtractMessageId(body);
            return MResult<string>.Success(messageId ?? string.Empty);
        }
        catch (Exception ex)
        {
            return MResult<string>.Fail(ex.Message);
        }
    }

    private static string? ExtractMessageId(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("messages", out var messagesElement) &&
                messagesElement.ValueKind == JsonValueKind.Array &&
                messagesElement.GetArrayLength() > 0)
            {
                var first = messagesElement[0];
                if (first.TryGetProperty("id", out var idProp))
                {
                    return idProp.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Respuesta inesperada de Meta: se ignora y se devuelve null (éxito sin id disponible).
        }

        return null;
    }
}
