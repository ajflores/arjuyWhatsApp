using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>Tamaño de página pedido a Meta al listar plantillas (<c>GET .../message_templates?limit=...</c>).</summary>
    private const int MessageTemplatesPageSize = 100;

    /// <summary>
    /// Límite de páginas a seguir en <see cref="GetMessageTemplatesAsync"/> antes de cortar, para no
    /// loopear indefinidamente ante un comportamiento inesperado de la API (por ejemplo, si Meta
    /// devolviera un <c>paging.next</c> que apunta en círculo). Con <see cref="MessageTemplatesPageSize"/>
    /// de 100, esto cubre hasta 5000 plantillas — muy por encima de lo esperable en la práctica.
    /// </summary>
    private const int MaxMessageTemplatesPages = 50;

    private static readonly JsonSerializerOptions _webhookJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ArjuyWhatsAppOptions _options;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ArjuyWhatsAppClient> _logger;
    private readonly IPhoneNumberNormalizer _phoneNumberNormalizer;

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
    /// <param name="phoneNumberNormalizer">
    /// Estrategia de normalización del número de destino antes de enviarlo a Meta. Se resuelve por
    /// DI — ver <see cref="IPhoneNumberNormalizer"/> y <see cref="ArjuyWhatsAppOptions.CountryCode"/>.
    /// </param>
    public ArjuyWhatsAppClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ArjuyWhatsAppOptions> options,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ArjuyWhatsAppClient> logger,
        IPhoneNumberNormalizer phoneNumberNormalizer)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _phoneNumberNormalizer = phoneNumberNormalizer;
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
            "location" => WhatsAppMessageType.Location,
            "contacts" => WhatsAppMessageType.Contacts,
            "reaction" => WhatsAppMessageType.Reaction,
            "audio" => WhatsAppMessageType.Audio,
            "video" => WhatsAppMessageType.Video,
            "sticker" => WhatsAppMessageType.Sticker,
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
                WhatsAppMessageType.Audio => inboundMessage.Audio?.Id,
                WhatsAppMessageType.Video => inboundMessage.Video?.Id,
                WhatsAppMessageType.Sticker => inboundMessage.Sticker?.Id,
                _ => null
            },
            IsVoiceNote = type == WhatsAppMessageType.Audio ? inboundMessage.Audio?.Voice ?? false : null,
            InteractiveReplyId = interactiveReply?.Id,
            InteractiveReplyTitle = interactiveReply?.Title,
            Location = type == WhatsAppMessageType.Location && inboundMessage.Location != null
                ? new WhatsAppReceivedLocation
                {
                    Latitude = inboundMessage.Location.Latitude,
                    Longitude = inboundMessage.Location.Longitude,
                    Name = inboundMessage.Location.Name,
                    Address = inboundMessage.Location.Address
                }
                : null,
            Contacts = type == WhatsAppMessageType.Contacts
                ? inboundMessage.Contacts.Select(MapToContact).ToList()
                : [],
            ReactionEmoji = type == WhatsAppMessageType.Reaction ? inboundMessage.Reaction?.Emoji ?? string.Empty : null,
            ReactionToMessageId = type == WhatsAppMessageType.Reaction ? inboundMessage.Reaction?.MessageId : null,
            Timestamp = ParseTimestamp(inboundMessage.Timestamp)
        };
    }

    private static WhatsAppContact MapToContact(WhatsAppWebhookContactContent source)
    {
        return new WhatsAppContact
        {
            Name = new WhatsAppContactName
            {
                FormattedName = source.Name.FormattedName,
                FirstName = source.Name.FirstName,
                LastName = source.Name.LastName,
                MiddleName = source.Name.MiddleName,
                Suffix = source.Name.Suffix,
                Prefix = source.Name.Prefix
            },
            Phones = source.Phones.Select(p => new WhatsAppContactPhone { Phone = p.Phone, Type = p.Type, WaId = p.WaId }).ToList(),
            Emails = source.Emails.Select(e => new WhatsAppContactEmail { Email = e.Email, Type = e.Type }).ToList(),
            Addresses = source.Addresses.Select(a => new WhatsAppContactAddress
            {
                Street = a.Street,
                City = a.City,
                State = a.State,
                Zip = a.Zip,
                Country = a.Country,
                CountryCode = a.CountryCode,
                Type = a.Type
            }).ToList(),
            Org = source.Org != null ? new WhatsAppContactOrg { Company = source.Org.Company, Department = source.Org.Department, Title = source.Org.Title } : null,
            Birthday = source.Birthday,
            Urls = source.Urls.Select(u => new WhatsAppContactUrl { Url = u.Url, Type = u.Type }).ToList()
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
    public async Task<MResult<string>> SendTextAsync(string phoneNumber, string message, string? replyToMessageId = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "text",
            text = new { body = message }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendTemplateAsync(string phoneNumber, string templateName, string languageCode, IEnumerable<string> parameters, string? replyToMessageId = null)
    {
        return await SendTemplateAsync(phoneNumber, templateName, languageCode, parameters, headerMedia: null, buttonParameters: null, replyToMessageId);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendTemplateAsync(string phoneNumber, string templateName, string languageCode, IEnumerable<string> bodyParameters, WhatsAppTemplateHeaderMedia? headerMedia, IEnumerable<WhatsAppTemplateButtonParameter>? buttonParameters = null, string? replyToMessageId = null)
    {
        if (headerMedia != null)
        {
            var hasLink = !string.IsNullOrWhiteSpace(headerMedia.Link);
            var hasMediaId = !string.IsNullOrWhiteSpace(headerMedia.MediaId);

            if (hasLink == hasMediaId)
            {
                return MResult<string>.Fail("WhatsAppTemplateHeaderMedia requiere exactamente uno entre Link y MediaId (no ambos, no ninguno).");
            }
        }

        var components = new List<object>();

        if (headerMedia != null)
        {
            components.Add(BuildTemplateHeaderComponent(headerMedia));
        }

        components.Add(new
        {
            type = "body",
            parameters = bodyParameters.Select(p => new { type = "text", text = p }).ToArray()
        });

        if (buttonParameters != null)
        {
            foreach (var button in buttonParameters)
            {
                components.Add(BuildTemplateButtonComponent(button));
            }
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components = components.ToArray()
            }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId);
    }

    /// <summary>
    /// Arma el componente <c>header</c> del payload de envío de un template a partir de
    /// <see cref="WhatsAppTemplateHeaderMedia"/> — <c>{"type":"header","parameters":[{"type":"image|video|document","image|video|document":{"link"|"id":"...", "filename"?:"..."}}]}</c>,
    /// verificado contra la documentación oficial de Meta (Cloud API, "Send Template Messages") al
    /// 2026-09-19. <c>filename</c> solo se incluye para header de tipo documento.
    /// </summary>
    private static object BuildTemplateHeaderComponent(WhatsAppTemplateHeaderMedia headerMedia)
    {
        var mediaTypeName = headerMedia.Type switch
        {
            WhatsAppTemplateHeaderMediaType.Image => "image",
            WhatsAppTemplateHeaderMediaType.Video => "video",
            WhatsAppTemplateHeaderMediaType.Document => "document",
            _ => throw new ArgumentOutOfRangeException(nameof(headerMedia), headerMedia.Type, "Tipo de header de plantilla no soportado.")
        };

        var mediaFields = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(headerMedia.Link))
        {
            mediaFields["link"] = headerMedia.Link;
        }
        else
        {
            mediaFields["id"] = headerMedia.MediaId;
        }

        if (headerMedia.Type == WhatsAppTemplateHeaderMediaType.Document)
        {
            mediaFields["filename"] = headerMedia.FileName ?? string.Empty;
        }

        var parameter = new Dictionary<string, object?>
        {
            ["type"] = mediaTypeName,
            [mediaTypeName] = mediaFields
        };

        return new
        {
            type = "header",
            parameters = new object[] { parameter }
        };
    }

    /// <summary>
    /// Arma el componente <c>button</c> del payload de envío de un template a partir de
    /// <see cref="WhatsAppTemplateButtonParameter"/> — <c>{"type":"button","sub_type":"url|quick_reply","index":N,"parameters":[{"type":"text|payload","text|payload":"valor"}]}</c>,
    /// verificado contra la documentación oficial de Meta al 2026-09-19.
    /// </summary>
    private static object BuildTemplateButtonComponent(WhatsAppTemplateButtonParameter button)
    {
        var (subTypeName, parameterType) = button.SubType switch
        {
            WhatsAppTemplateButtonSubType.Url => ("url", "text"),
            WhatsAppTemplateButtonSubType.QuickReply => ("quick_reply", "payload"),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button.SubType, "Sub-tipo de botón de plantilla no soportado.")
        };

        var parameter = new Dictionary<string, object?>
        {
            ["type"] = parameterType,
            [parameterType] = button.Value
        };

        return new
        {
            type = "button",
            sub_type = subTypeName,
            index = button.Index,
            parameters = new object[] { parameter }
        };
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendImageAsync(string phoneNumber, string imageUrl, string? caption = null, string? replyToMessageId = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "image",
            image = new { link = imageUrl, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendDocumentAsync(string phoneNumber, string documentUrl, string fileName, string? caption = null, string? replyToMessageId = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "document",
            document = new { link = documentUrl, filename = fileName, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId);
    }

    /// <inheritdoc />
    public async Task<MResult<byte[]>> DownloadMediaAsync(string mediaId)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            return MResult<byte[]>.Fail("ArjuyWhatsApp no está configurado: falta AccessToken.");
        }

        return await ExecuteWithRetryAsync<byte[]>(async () =>
        {
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
                    return (MResult<byte[]>.Fail(ParseMetaError(metaResponse.StatusCode, metaBody)), GetRetryAfterDelay(metaResponse));
                }

                using var metaDoc = JsonDocument.Parse(metaBody);
                if (!metaDoc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    return (MResult<byte[]>.Fail("Meta no devolvió URL de descarga para el media id indicado."), (TimeSpan?)null);
                }

                var downloadUrl = urlProp.GetString();
                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return (MResult<byte[]>.Fail("Meta devolvió una URL de descarga vacía."), (TimeSpan?)null);
                }

                // Paso 2: descargar el archivo binario usando el mismo token.
                using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                downloadRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

                using var downloadResponse = await client.SendAsync(downloadRequest);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    var errorBody = await downloadResponse.Content.ReadAsStringAsync();
                    return (MResult<byte[]>.Fail(ParseMetaError(downloadResponse.StatusCode, errorBody)), GetRetryAfterDelay(downloadResponse));
                }

                var data = await downloadResponse.Content.ReadAsByteArrayAsync();
                return (MResult<byte[]>.Success(data), (TimeSpan?)null);
            }
            catch (Exception ex)
            {
                return (MResult<byte[]>.Fail(ex.Message), (TimeSpan?)null);
            }
        });
    }

    /// <inheritdoc />
    public async Task<MResult<string>> UploadMediaAsync(byte[] fileContent, string fileName, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            return MResult<string>.Fail("ArjuyWhatsApp no está configurado: falta AccessToken o PhoneNumberId.");
        }

        return await ExecuteWithRetryAsync<string>(async () =>
        {
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
                    return (MResult<string>.Fail(ParseMetaError(response.StatusCode, body)), GetRetryAfterDelay(response));
                }

                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    return (MResult<string>.Fail("Meta no devolvió un media id para el archivo subido."), (TimeSpan?)null);
                }

                return (MResult<string>.Success(idProp.GetString() ?? string.Empty), (TimeSpan?)null);
            }
            catch (Exception ex)
            {
                return (MResult<string>.Fail(ex.Message), (TimeSpan?)null);
            }
        });
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendImageByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null, string? replyToMessageId = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "image",
            image = new { id = mediaId, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendDocumentByMediaIdAsync(string phoneNumber, string mediaId, string? fileName = null, string? caption = null, string? replyToMessageId = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "document",
            document = new { id = mediaId, filename = fileName ?? string.Empty, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendAudioAsync(string phoneNumber, string audioUrl, bool voice = false, string? replyToMessageId = null, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "audio",
            audio = new { link = audioUrl, voice }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendAudioByMediaIdAsync(string phoneNumber, string mediaId, bool voice = false, string? replyToMessageId = null, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "audio",
            audio = new { id = mediaId, voice }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendVideoAsync(string phoneNumber, string videoUrl, string? caption = null, string? replyToMessageId = null, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "video",
            video = new { link = videoUrl, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendVideoByMediaIdAsync(string phoneNumber, string mediaId, string? caption = null, string? replyToMessageId = null, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "video",
            video = new { id = mediaId, caption = caption ?? string.Empty }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendStickerAsync(string phoneNumber, string stickerUrl, string? replyToMessageId = null, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "sticker",
            sticker = new { link = stickerUrl }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendStickerByMediaIdAsync(string phoneNumber, string mediaId, string? replyToMessageId = null, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "sticker",
            sticker = new { id = mediaId }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendInteractiveButtonsAsync(string phoneNumber, string bodyText, IEnumerable<(string Id, string Title)> buttons, string? replyToMessageId = null)
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
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
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

        return await SendMessagePayloadAsync(payload, replyToMessageId);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendInteractiveListAsync(string phoneNumber, string bodyText, string buttonText, IEnumerable<(string SectionTitle, IEnumerable<(string Id, string Title, string? Description)> Rows)> sections, string? replyToMessageId = null)
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
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
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

        return await SendMessagePayloadAsync(payload, replyToMessageId);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendReactionAsync(string phoneNumber, string messageId, string emoji, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "reaction",
            reaction = new { message_id = messageId, emoji }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendLocationAsync(string phoneNumber, double latitude, double longitude, string? name = null, string? address = null, string? replyToMessageId = null, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "location",
            location = new
            {
                latitude,
                longitude,
                name = name ?? string.Empty,
                address = address ?? string.Empty
            }
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MResult<string>> SendContactsAsync(string phoneNumber, IEnumerable<WhatsAppContact> contacts, string? replyToMessageId = null, CancellationToken cancellationToken = default)
    {
        var contactList = contacts.ToList();

        if (contactList.Count == 0)
        {
            return MResult<string>.Fail("SendContactsAsync requiere al menos un contacto.");
        }

        foreach (var contact in contactList)
        {
            if (string.IsNullOrWhiteSpace(contact.Name?.FormattedName))
            {
                return MResult<string>.Fail("Cada contacto requiere Name.FormattedName no vacío.");
            }
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = _phoneNumberNormalizer.Normalize(phoneNumber),
            type = "contacts",
            contacts = contactList.Select(BuildContactPayload).ToArray()
        };

        return await SendMessagePayloadAsync(payload, replyToMessageId, cancellationToken);
    }

    /// <summary>
    /// Arma el objeto de un contacto individual del array <c>contacts</c> del payload de envío,
    /// omitiendo secciones vacías/nulas (Meta no exige ningún campo salvo <c>name.formatted_name</c>,
    /// ya validado por el llamador). Formato verificado contra la documentación oficial de Meta
    /// (Cloud API, "Contacts Messages") al 2026-09-19.
    /// </summary>
    private static object BuildContactPayload(WhatsAppContact contact)
    {
        var result = new Dictionary<string, object?>
        {
            ["name"] = new
            {
                formatted_name = contact.Name.FormattedName,
                first_name = contact.Name.FirstName,
                last_name = contact.Name.LastName,
                middle_name = contact.Name.MiddleName,
                suffix = contact.Name.Suffix,
                prefix = contact.Name.Prefix
            }
        };

        if (contact.Phones.Count > 0)
        {
            result["phones"] = contact.Phones
                .Select(p => new { phone = p.Phone, type = p.Type, wa_id = p.WaId })
                .ToArray();
        }

        if (contact.Emails.Count > 0)
        {
            result["emails"] = contact.Emails
                .Select(e => new { email = e.Email, type = e.Type })
                .ToArray();
        }

        if (contact.Addresses.Count > 0)
        {
            result["addresses"] = contact.Addresses
                .Select(a => new
                {
                    street = a.Street,
                    city = a.City,
                    state = a.State,
                    zip = a.Zip,
                    country = a.Country,
                    country_code = a.CountryCode,
                    type = a.Type
                })
                .ToArray();
        }

        if (contact.Org != null)
        {
            result["org"] = new
            {
                company = contact.Org.Company,
                department = contact.Org.Department,
                title = contact.Org.Title
            };
        }

        if (!string.IsNullOrWhiteSpace(contact.Birthday))
        {
            result["birthday"] = contact.Birthday;
        }

        if (contact.Urls.Count > 0)
        {
            result["urls"] = contact.Urls
                .Select(u => new { url = u.Url, type = u.Type })
                .ToArray();
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<MResult<IReadOnlyList<WhatsAppMessageTemplate>>> GetMessageTemplatesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.BusinessAccountId))
        {
            return MResult<IReadOnlyList<WhatsAppMessageTemplate>>.Fail("ArjuyWhatsApp no está configurado: falta AccessToken o BusinessAccountId.");
        }

        var templates = new List<WhatsAppMessageTemplate>();
        var nextUrl = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.BusinessAccountId}/message_templates?limit={MessageTemplatesPageSize}";

        for (var page = 0; page < MaxMessageTemplatesPages && nextUrl != null; page++)
        {
            var pageResult = await ExecuteWithRetryAsync<(List<WhatsAppMessageTemplate> Templates, string? NextUrl)>(async () =>
            {
                try
                {
                    var client = _httpClientFactory.CreateClient(HttpClientName);

                    using var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

                    using var response = await client.SendAsync(request, cancellationToken);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        return ((MResult<(List<WhatsAppMessageTemplate>, string?)>.Fail(ParseMetaError(response.StatusCode, body)), GetRetryAfterDelay(response)));
                    }

                    var (pageTemplates, pageNextUrl) = ParseMessageTemplatesPage(body);
                    return (MResult<(List<WhatsAppMessageTemplate>, string?)>.Success((pageTemplates, pageNextUrl)), (TimeSpan?)null);
                }
                catch (Exception ex)
                {
                    return (MResult<(List<WhatsAppMessageTemplate>, string?)>.Fail(ex.Message), (TimeSpan?)null);
                }
            }, cancellationToken);

            if (!pageResult.IsSuccess)
            {
                return pageResult.Error != null
                    ? MResult<IReadOnlyList<WhatsAppMessageTemplate>>.Fail(pageResult.Error)
                    : MResult<IReadOnlyList<WhatsAppMessageTemplate>>.Fail(pageResult.Message ?? "Error desconocido al listar plantillas.");
            }

            templates.AddRange(pageResult.Data!.Templates);
            nextUrl = pageResult.Data!.NextUrl;
        }

        return MResult<IReadOnlyList<WhatsAppMessageTemplate>>.Success(templates);
    }

    /// <summary>
    /// Parsea una página de la respuesta de <c>GET /{business-account-id}/message_templates</c>:
    /// la lista de plantillas de <c>data[]</c> y la URL de la página siguiente en
    /// <c>paging.next</c> (<c>null</c> si no hay más páginas).
    /// </summary>
    private static (List<WhatsAppMessageTemplate> Templates, string? NextUrl) ParseMessageTemplatesPage(string body)
    {
        var templates = new List<WhatsAppMessageTemplate>();

        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataElement.EnumerateArray())
            {
                templates.Add(ParseMessageTemplate(item));
            }
        }

        string? nextUrl = null;
        if (doc.RootElement.TryGetProperty("paging", out var pagingElement) &&
            pagingElement.TryGetProperty("next", out var nextProp) &&
            nextProp.ValueKind == JsonValueKind.String)
        {
            nextUrl = nextProp.GetString();
        }

        return (templates, nextUrl);
    }

    private static WhatsAppMessageTemplate ParseMessageTemplate(JsonElement item)
    {
        var status = item.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var components = new List<WhatsAppTemplateComponent>();

        if (item.TryGetProperty("components", out var componentsElement) && componentsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var componentElement in componentsElement.EnumerateArray())
            {
                components.Add(ParseTemplateComponent(componentElement));
            }
        }

        return new WhatsAppMessageTemplate
        {
            Id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
            Name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty,
            Language = item.TryGetProperty("language", out var languageProp) ? languageProp.GetString() ?? string.Empty : string.Empty,
            Category = item.TryGetProperty("category", out var categoryProp) ? categoryProp.GetString() ?? string.Empty : string.Empty,
            Status = MapTemplateStatus(status),
            RejectedReason = item.TryGetProperty("rejected_reason", out var rejectedProp) && rejectedProp.ValueKind == JsonValueKind.String
                ? rejectedProp.GetString()
                : null,
            Components = components
        };
    }

    private static WhatsAppTemplateComponent ParseTemplateComponent(JsonElement componentElement)
    {
        var type = componentElement.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
        var text = componentElement.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String
            ? textProp.GetString()
            : null;

        var buttons = new List<WhatsAppTemplateButton>();
        if (componentElement.TryGetProperty("buttons", out var buttonsElement) && buttonsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var buttonElement in buttonsElement.EnumerateArray())
            {
                buttons.Add(new WhatsAppTemplateButton
                {
                    Type = buttonElement.TryGetProperty("type", out var buttonTypeProp) ? buttonTypeProp.GetString() ?? string.Empty : string.Empty,
                    Text = buttonElement.TryGetProperty("text", out var buttonTextProp) ? buttonTextProp.GetString() ?? string.Empty : string.Empty,
                    Url = buttonElement.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String ? urlProp.GetString() : null,
                    PhoneNumber = buttonElement.TryGetProperty("phone_number", out var phoneProp) && phoneProp.ValueKind == JsonValueKind.String ? phoneProp.GetString() : null
                });
            }
        }

        return new WhatsAppTemplateComponent
        {
            Type = type switch
            {
                "HEADER" => WhatsAppTemplateComponentType.Header,
                "BODY" => WhatsAppTemplateComponentType.Body,
                "FOOTER" => WhatsAppTemplateComponentType.Footer,
                "BUTTONS" => WhatsAppTemplateComponentType.Buttons,
                _ => WhatsAppTemplateComponentType.Unknown
            },
            Format = componentElement.TryGetProperty("format", out var formatProp) && formatProp.ValueKind == JsonValueKind.String ? formatProp.GetString() : null,
            Text = text,
            ParameterCount = CountPositionalParameters(text),
            Buttons = buttons
        };
    }

    /// <summary>
    /// Cuenta placeholders posicionales distintos (<c>{{1}}</c>, <c>{{2}}</c>, ...) en el texto de
    /// un componente de plantilla. Devuelve el placeholder de mayor número encontrado (no la
    /// cantidad de ocurrencias) — es lo que determina cuántos <c>parameters</c> espera Meta, ya que
    /// los placeholders deben usarse en orden consecutivo desde <c>{{1}}</c>.
    /// </summary>
    private static int CountPositionalParameters(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var maxIndex = 0;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, @"\{\{(\d+)\}\}"))
        {
            if (int.TryParse(match.Groups[1].Value, out var index) && index > maxIndex)
            {
                maxIndex = index;
            }
        }

        return maxIndex;
    }

    private static WhatsAppMessageTemplateStatus MapTemplateStatus(string? status) => status switch
    {
        "APPROVED" => WhatsAppMessageTemplateStatus.Approved,
        "PENDING" => WhatsAppMessageTemplateStatus.Pending,
        "REJECTED" => WhatsAppMessageTemplateStatus.Rejected,
        "PAUSED" => WhatsAppMessageTemplateStatus.Paused,
        "DISABLED" => WhatsAppMessageTemplateStatus.Disabled,
        _ => WhatsAppMessageTemplateStatus.Unknown
    };

    /// <inheritdoc />
    public async Task<MResult<bool>> MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = messageId
        };

        return await SendStatusPayloadAsync(payload, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MResult<bool>> MarkAsReadWithTypingIndicatorAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = messageId,
            typing_indicator = new { type = "text" }
        };

        return await SendStatusPayloadAsync(payload, cancellationToken);
    }

    /// <summary>
    /// Envía un payload de estado (<c>status: "read"</c>, con o sin <c>typing_indicator</c>) al mismo
    /// endpoint <c>POST /{phone-number-id}/messages</c> que usan los métodos de envío, pero a
    /// diferencia de <see cref="SendMessagePayloadAsync"/> la respuesta exitosa de Meta no trae un
    /// <c>messages[0].id</c> (trae <c>{"success": true}</c>) — por eso este helper devuelve
    /// <see cref="MResult{T}"/> de <see cref="bool"/> en vez de intentar extraer un message id que no
    /// existe en este tipo de respuesta.
    /// </summary>
    private async Task<MResult<bool>> SendStatusPayloadAsync(object payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            return MResult<bool>.Fail("ArjuyWhatsApp no está configurado: falta AccessToken o PhoneNumberId.");
        }

        return await ExecuteWithRetryAsync<bool>(async () =>
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var url = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.PhoneNumberId}/messages";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);
                request.Content = JsonContent.Create(payload);

                using var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return (MResult<bool>.Fail(ParseMetaError(response.StatusCode, body)), GetRetryAfterDelay(response));
                }

                return (MResult<bool>.Success(true), (TimeSpan?)null);
            }
            catch (Exception ex)
            {
                return (MResult<bool>.Fail(ex.Message), (TimeSpan?)null);
            }
        }, cancellationToken);
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Agrega el componente <c>context.message_id</c> al nivel superior del payload de envío cuando
    /// <paramref name="replyToMessageId"/> viene informado, para que Meta muestre el mensaje enviado
    /// como respuesta/cita de un mensaje entrante anterior en el chat del destinatario — formato
    /// verificado contra la documentación oficial de Meta (Cloud API, "Contextual Replies") al
    /// 2026-09-19: <c>context</c> es genérico, va al mismo nivel que <c>to</c>/<c>type</c> sin
    /// importar el tipo de mensaje (texto, template, media, interactivo). Si
    /// <paramref name="replyToMessageId"/> es <c>null</c> o vacío, devuelve <paramref name="payload"/>
    /// sin modificar (sin campo <c>context</c> en el body enviado).
    /// </summary>
    private static object BuildRequestPayload(object payload, string? replyToMessageId)
    {
        if (string.IsNullOrWhiteSpace(replyToMessageId))
        {
            return payload;
        }

        var node = JsonSerializer.SerializeToNode(payload) as JsonObject
            ?? throw new InvalidOperationException("El payload de envío no se pudo serializar a un objeto JSON.");

        node["context"] = new JsonObject { ["message_id"] = replyToMessageId };

        return node;
    }

    private async Task<MResult<string>> SendMessagePayloadAsync(object payload, string? replyToMessageId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            return MResult<string>.Fail("ArjuyWhatsApp no está configurado: falta AccessToken o PhoneNumberId.");
        }

        return await ExecuteWithRetryAsync<string>(async () =>
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var url = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.PhoneNumberId}/messages";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);
                request.Content = JsonContent.Create(BuildRequestPayload(payload, replyToMessageId));

                using var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return (MResult<string>.Fail(ParseMetaError(response.StatusCode, body)), GetRetryAfterDelay(response));
                }

                var messageId = ExtractMessageId(body);
                return (MResult<string>.Success(messageId ?? string.Empty), (TimeSpan?)null);
            }
            catch (Exception ex)
            {
                return (MResult<string>.Fail(ex.Message), (TimeSpan?)null);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Ejecuta <paramref name="operation"/> con retry y backoff exponencial: si el resultado es un
    /// fallo cuyo <see cref="MetaApiError.IsTransient"/> es <c>true</c> (rate limiting HTTP 429 o
    /// error 5xx de Meta), reintenta hasta <see cref="ArjuyWhatsAppOptions.MaxRetryAttempts"/> veces
    /// antes de devolver el último resultado tal cual. Errores no transitorios (400, 401, 403,
    /// validaciones locales sin <see cref="MetaApiError"/>) se devuelven en el primer intento, sin
    /// reintentar. <paramref name="operation"/> devuelve, junto con el <see cref="MResult{T}"/>, un
    /// delay opcional tomado del header <c>Retry-After</c> de la respuesta de Meta — cuando viene
    /// presente, se usa ese delay exacto en vez del backoff calculado.
    /// </summary>
    private async Task<MResult<T>> ExecuteWithRetryAsync<T>(Func<Task<(MResult<T> Result, TimeSpan? RetryAfter)>> operation, CancellationToken cancellationToken = default)
    {
        var retryIndex = 0;

        while (true)
        {
            var (result, retryAfter) = await operation();

            if (result.IsSuccess || result.Error?.IsTransient != true || retryIndex >= _options.MaxRetryAttempts)
            {
                return result;
            }

            var delay = retryAfter ?? ComputeBackoffDelay(retryIndex);
            retryIndex++;

            _logger.LogWarning(
                "ArjuyWhatsApp: reintento {RetryIndex}/{MaxRetryAttempts} tras error transitorio de Meta (HTTP {StatusCode}) — esperando {DelayMs}ms",
                retryIndex, _options.MaxRetryAttempts, result.Error!.HttpStatusCode, delay.TotalMilliseconds);

            await Task.Delay(delay, cancellationToken);
        }
    }

    /// <summary>
    /// Calcula el delay del reintento número <paramref name="retryIndex"/> (0-indexado) con backoff
    /// exponencial en base a <see cref="ArjuyWhatsAppOptions.BaseRetryDelay"/> (<c>BaseRetryDelay *
    /// 2^retryIndex</c>), con jitter aleatorio de ±20% para evitar que múltiples instancias
    /// reintenten todas al mismo tiempo (efecto "thundering herd").
    /// </summary>
    private TimeSpan ComputeBackoffDelay(int retryIndex)
    {
        var exponentialMs = _options.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, retryIndex);
        var jitterFactor = 1 + ((Random.Shared.NextDouble() * 0.4) - 0.2); // uniforme en [0.8, 1.2]
        return TimeSpan.FromMilliseconds(exponentialMs * jitterFactor);
    }

    /// <summary>
    /// Extrae el delay del header <c>Retry-After</c> de una respuesta HTTP, si vino presente —
    /// soporta tanto la forma en segundos (<c>Retry-After: 30</c>) como la de fecha HTTP
    /// (<c>Retry-After: Wed, 21 Oct 2026 07:28:00 GMT</c>). Cuando Meta lo manda en una respuesta
    /// 429, tiene prioridad sobre el backoff calculado — es la propia Meta indicando cuánto esperar.
    /// </summary>
    private static TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null)
        {
            return null;
        }

        if (retryAfter.Delta.HasValue)
        {
            return retryAfter.Delta.Value;
        }

        if (retryAfter.Date.HasValue)
        {
            var delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    /// <summary>
    /// Parsea el objeto <c>error</c> del body de una respuesta no exitosa de la Graph API de Meta en
    /// un <see cref="MetaApiError"/>. Tolerante a que el body no sea JSON válido o no tenga la forma
    /// esperada (por ejemplo, un error de infraestructura de Meta que devuelva HTML o texto plano en
    /// vez del JSON documentado) — en esos casos devuelve un <see cref="MetaApiError"/> con
    /// <see cref="MetaApiError.Message"/> igual al body crudo y el resto de los campos en <c>null</c>,
    /// para no perder la información aunque no se pueda estructurar.
    /// </summary>
    private static MetaApiError ParseMetaError(HttpStatusCode statusCode, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                var message = errorElement.TryGetProperty("message", out var messageProp)
                    ? messageProp.GetString() ?? body
                    : body;
                var code = errorElement.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.Number
                    ? codeProp.GetInt32()
                    : (int?)null;
                var errorSubcode = errorElement.TryGetProperty("error_subcode", out var subcodeProp) && subcodeProp.ValueKind == JsonValueKind.Number
                    ? subcodeProp.GetInt32()
                    : (int?)null;
                var type = errorElement.TryGetProperty("type", out var typeProp)
                    ? typeProp.GetString()
                    : null;
                var fbTraceId = errorElement.TryGetProperty("fbtrace_id", out var traceProp)
                    ? traceProp.GetString()
                    : null;

                return new MetaApiError((int)statusCode, body, message, code, errorSubcode, type, fbTraceId);
            }
        }
        catch (JsonException)
        {
            // Body no es JSON válido (o no tiene la forma esperada) — se cae al fallback de abajo.
        }

        return new MetaApiError((int)statusCode, body, body);
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
