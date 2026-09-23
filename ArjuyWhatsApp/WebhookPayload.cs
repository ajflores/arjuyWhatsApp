using System.Text.Json.Serialization;

namespace ArjuyWhatsApp;

// -----------------------------------------------------------------------
// DTOs internos para deserializar el payload crudo que Meta manda al webhook de WhatsApp Cloud
// API (entry[].changes[].value.messages[] / contacts[] / metadata). La forma fue verificada
// contra una implementación real de producción (WebhooksController de ArjuyTurismo), no
// inventada — Meta no publica un schema formal para esto.
// -----------------------------------------------------------------------

internal class WhatsAppWebhookPayload
{
    public string Object { get; set; } = string.Empty;
    public List<WhatsAppWebhookEntry> Entry { get; set; } = [];
}

internal class WhatsAppWebhookEntry
{
    public string Id { get; set; } = string.Empty;
    public List<WhatsAppWebhookChange> Changes { get; set; } = [];
}

internal class WhatsAppWebhookChange
{
    public WhatsAppWebhookValue? Value { get; set; }
    public string Field { get; set; } = string.Empty;
}

internal class WhatsAppWebhookValue
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = string.Empty;
    public List<WhatsAppWebhookContact> Contacts { get; set; } = [];
    public List<WhatsAppWebhookInboundMessage> Messages { get; set; } = [];

    /// <summary>
    /// Actualizaciones de estado de entrega de mensajes salientes (sent/delivered/read/failed).
    /// Un webhook de Meta trae <see cref="Messages"/> o <see cref="Statuses"/>, nunca ambos a la
    /// vez — pero el código que consume esto igual chequea los dos por las dudas.
    /// </summary>
    public List<WhatsAppWebhookStatus> Statuses { get; set; } = [];
}

internal class WhatsAppWebhookContact
{
    public WhatsAppWebhookProfile? Profile { get; set; }
    [JsonPropertyName("wa_id")]
    public string WaId { get; set; } = string.Empty;
}

internal class WhatsAppWebhookProfile
{
    public string Name { get; set; } = string.Empty;
}

internal class WhatsAppWebhookInboundMessage
{
    public string From { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public WhatsAppWebhookTextContent? Text { get; set; }
    public WhatsAppWebhookImageContent? Image { get; set; }
    public WhatsAppWebhookDocumentContent? Document { get; set; }
    public WhatsAppWebhookInteractiveContent? Interactive { get; set; }
    public WhatsAppWebhookLocationContent? Location { get; set; }
    public List<WhatsAppWebhookContactContent> Contacts { get; set; } = [];
    public WhatsAppWebhookReactionContent? Reaction { get; set; }
    public WhatsAppWebhookAudioContent? Audio { get; set; }
    public WhatsAppWebhookVideoContent? Video { get; set; }
    public WhatsAppWebhookStickerContent? Sticker { get; set; }
    public string Type { get; set; } = string.Empty;
}

// -----------------------------------------------------------------------
// DTOs internos para deserializar mensajes entrantes de tipo "location", "contacts" y "reaction" —
// mismo formato (a nivel de nombres de campo) que el payload de ENVÍO documentado por Meta para
// estos tipos, ya que Meta lo reusa simétricamente para lo entrante (Cloud API, "Location
// Messages" / "Contacts Messages" / "Reaction Messages"), verificado al 2026-09-19.
// -----------------------------------------------------------------------

internal class WhatsAppWebhookLocationContent
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
}

internal class WhatsAppWebhookReactionContent
{
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
}

internal class WhatsAppWebhookContactContent
{
    public WhatsAppWebhookContactName Name { get; set; } = new();
    public List<WhatsAppWebhookContactPhone> Phones { get; set; } = [];
    public List<WhatsAppWebhookContactEmail> Emails { get; set; } = [];
    public List<WhatsAppWebhookContactAddress> Addresses { get; set; } = [];
    public WhatsAppWebhookContactOrg? Org { get; set; }
    public string? Birthday { get; set; }
    public List<WhatsAppWebhookContactUrl> Urls { get; set; } = [];
}

internal class WhatsAppWebhookContactName
{
    [JsonPropertyName("formatted_name")]
    public string FormattedName { get; set; } = string.Empty;
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
    [JsonPropertyName("middle_name")]
    public string? MiddleName { get; set; }
    public string? Suffix { get; set; }
    public string? Prefix { get; set; }
}

internal class WhatsAppWebhookContactPhone
{
    public string Phone { get; set; } = string.Empty;
    public string? Type { get; set; }
    [JsonPropertyName("wa_id")]
    public string? WaId { get; set; }
}

internal class WhatsAppWebhookContactEmail
{
    public string Email { get; set; } = string.Empty;
    public string? Type { get; set; }
}

internal class WhatsAppWebhookContactAddress
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }
    public string? Type { get; set; }
}

internal class WhatsAppWebhookContactOrg
{
    public string? Company { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
}

internal class WhatsAppWebhookContactUrl
{
    public string Url { get; set; } = string.Empty;
    public string? Type { get; set; }
}

internal class WhatsAppWebhookTextContent
{
    public string Body { get; set; } = string.Empty;
}

internal class WhatsAppWebhookImageContent
{
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

internal class WhatsAppWebhookDocumentContent
{
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = string.Empty;
    [JsonPropertyName("filename")]
    public string? FileName { get; set; }
    public string? Caption { get; set; }
}

// Formato de audio/video/sticker entrantes verificado contra developers.facebook.com (Cloud API,
// "Webhooks > Components" y las páginas individuales de cada tipo de media) al 2026-09-19.

internal class WhatsAppWebhookAudioContent
{
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = string.Empty;
    public bool? Voice { get; set; }
}

internal class WhatsAppWebhookVideoContent
{
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

internal class WhatsAppWebhookStickerContent
{
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = string.Empty;
    public bool? Animated { get; set; }
}

// -----------------------------------------------------------------------
// DTOs internos para deserializar la respuesta a un mensaje interactivo (botón de respuesta
// rápida o fila de lista) que llega en entry[].changes[].value.messages[] con type "interactive".
// Forma confirmada contra la documentación oficial de Meta (WhatsApp Cloud API, Interactive
// Reply Buttons Messages e Interactive List Messages) al 2026-09-17: el webhook trae
// interactive.type "button_reply" con { id, title }, o interactive.type "list_reply" con
// { id, title, description } — esta librería solo mapea id/title (ver
// WhatsAppMessageReceived.InteractiveReplyId/InteractiveReplyTitle); description no se expone
// porque no aporta nada que el consumidor no pueda resolver a partir del id.
// -----------------------------------------------------------------------

internal class WhatsAppWebhookInteractiveContent
{
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("button_reply")]
    public WhatsAppWebhookInteractiveReply? ButtonReply { get; set; }

    [JsonPropertyName("list_reply")]
    public WhatsAppWebhookInteractiveReply? ListReply { get; set; }
}

internal class WhatsAppWebhookInteractiveReply
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// -----------------------------------------------------------------------
// DTOs internos para deserializar entry[].changes[].value.statuses[] — las actualizaciones de
// estado de entrega de mensajes salientes (sent/delivered/read/failed). Forma confirmada contra
// la documentación oficial de Meta (WhatsApp Cloud API, Webhooks > Components,
// https://developers.facebook.com/docs/whatsapp/cloud-api/webhooks/components) al 2026-09-17: el
// ejemplo publicado ahí muestra un status "delivered" con exactamente estos campos (más
// "conversation"/"pricing", que esta librería no necesita y por eso no mapea). La forma del
// array "errors" para status "failed" no tiene un ejemplo completo publicado en esa página ni en
// la de error codes; se usa la forma estándar y estable del objeto de error de la Graph API de
// Meta (code/title/message/error_data.details), consistente con el objeto de error genérico que
// sí está documentado.
// -----------------------------------------------------------------------

internal class WhatsAppWebhookStatus
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    [JsonPropertyName("recipient_id")]
    public string RecipientId { get; set; } = string.Empty;
    public List<WhatsAppWebhookStatusError> Errors { get; set; } = [];
}

internal class WhatsAppWebhookStatusError
{
    public int Code { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
}
