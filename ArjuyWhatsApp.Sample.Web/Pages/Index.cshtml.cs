using ArjuyWhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArjuyWhatsApp.Sample.Web.Pages;

/// <summary>
/// Página única de la sample: un formulario por funcionalidad de <see cref="IArjuyWhatsAppClient"/>
/// (texto, botones interactivos, imagen, documento, audio, video, sticker, ubicación, contacto,
/// reacción, listado de plantillas), cada uno como un named page handler (patrón PRG), más el
/// listado de mensajes recibidos leídos desde <see cref="IMessageStore"/>.
/// </summary>
public class IndexModel : PageModel
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;
    private readonly IMessageStore _messageStore;

    public IndexModel(IArjuyWhatsAppClient whatsAppClient, IMessageStore messageStore)
    {
        _whatsAppClient = whatsAppClient;
        _messageStore = messageStore;
    }

    [BindProperty]
    public SendTextInput Input { get; set; } = new();

    [BindProperty]
    public SendConfirmationInput Confirmation { get; set; } = new();

    [BindProperty]
    public SendImageInput Image { get; set; } = new();

    [BindProperty]
    public SendDocumentInput Document { get; set; } = new();

    [BindProperty]
    public SendAudioInput Audio { get; set; } = new();

    [BindProperty]
    public SendVideoInput Video { get; set; } = new();

    [BindProperty]
    public SendStickerInput Sticker { get; set; } = new();

    [BindProperty]
    public SendLocationInput Location { get; set; } = new();

    [BindProperty]
    public SendContactInput Contact { get; set; } = new();

    [BindProperty]
    public SendReactionInput Reaction { get; set; } = new();

    /// <summary>Resultado del último envío (éxito o error), para mostrar en la misma página tras el POST.</summary>
    public MResult<string>? SendResult { get; private set; }

    /// <summary>Resultado del último listado de plantillas, si se pidió.</summary>
    public MResult<IReadOnlyList<WhatsAppMessageTemplate>>? TemplatesResult { get; private set; }

    /// <summary>Mensajes recibidos hasta ahora, más nuevo primero.</summary>
    public IReadOnlyList<WhatsAppMessageReceived> ReceivedMessages { get; private set; } = Array.Empty<WhatsAppMessageReceived>();

    public class SendTextInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    public class SendConfirmationInput
    {
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class SendImageInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public IFormFile? File { get; set; }

        public string? Caption { get; set; }
    }

    public class SendDocumentInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public IFormFile? File { get; set; }

        /// <summary>Nombre de archivo a mostrar al destinatario. Si se deja vacío, se usa el nombre real del archivo subido.</summary>
        public string? FileName { get; set; }

        public string? Caption { get; set; }
    }

    public class SendAudioInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public IFormFile? File { get; set; }

        public bool Voice { get; set; }
    }

    public class SendVideoInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public IFormFile? File { get; set; }

        public string? Caption { get; set; }
    }

    public class SendStickerInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public IFormFile? File { get; set; }
    }

    public class SendLocationInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string? PlaceName { get; set; }

        public string? Address { get; set; }
    }

    public class SendContactInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public string ContactName { get; set; } = string.Empty;

        public string? ContactPhone { get; set; }
    }

    public class SendReactionInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public string MessageId { get; set; } = string.Empty;

        public string Emoji { get; set; } = string.Empty;
    }

    public void OnGet()
    {
        ReceivedMessages = _messageStore.GetAll();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ReceivedMessages = _messageStore.GetAll();
            return Page();
        }

        // 🔴 PONÉ UN BREAKPOINT ACÁ para ver el Input antes de mandarlo, y en la línea de abajo
        // para ver el MResult de la respuesta.
        SendResult = await _whatsAppClient.SendTextAsync(Input.PhoneNumber, Input.Message);

        // No se hace redirect (PRG completo) a propósito: así el resultado del envío (éxito/error)
        // sigue disponible para mostrarse en la misma respuesta, sin tener que pasarlo por
        // TempData. El listado de recibidos se vuelve a leer igual, para que quede actualizado.
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    /// <summary>
    /// Manda un mensaje interactivo con dos botones fijos ("Confirmar" / "Cancelar") — caso de uso
    /// real de chat: confirmar una acción sin que el cliente tenga que escribir texto libre. Cuando
    /// el destinatario toca un botón, la respuesta llega como un mensaje entrante normal (ver
    /// <see cref="WhatsAppMessageReceived.InteractiveReplyId"/>), y <see cref="StoringMessageHandler"/>
    /// la guarda y la muestra en la tabla de recibidos igual que cualquier otro mensaje.
    /// </summary>
    public async Task<IActionResult> OnPostConfirmationAsync()
    {
        if (string.IsNullOrWhiteSpace(Confirmation.PhoneNumber))
        {
            ReceivedMessages = _messageStore.GetAll();
            return Page();
        }

        SendResult = await _whatsAppClient.SendInteractiveButtonsAsync(
            Confirmation.PhoneNumber,
            "¿Confirmás tu pedido?",
            new (string Id, string Title)[]
            {
                ("confirmar", "Confirmar"),
                ("cancelar", "Cancelar"),
            });

        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    public async Task<IActionResult> OnPostImageAsync(CancellationToken cancellationToken)
    {
        SendResult = await UploadAndSendAsync(
            Image.File,
            cancellationToken,
            mediaId => _whatsAppClient.SendImageByMediaIdAsync(Image.PhoneNumber, mediaId, Image.Caption));
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    public async Task<IActionResult> OnPostDocumentAsync(CancellationToken cancellationToken)
    {
        SendResult = await UploadAndSendAsync(
            Document.File,
            cancellationToken,
            mediaId => _whatsAppClient.SendDocumentByMediaIdAsync(
                Document.PhoneNumber, mediaId, Document.FileName ?? Document.File?.FileName, Document.Caption));
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    public async Task<IActionResult> OnPostAudioAsync(CancellationToken cancellationToken)
    {
        SendResult = await UploadAndSendAsync(
            Audio.File,
            cancellationToken,
            mediaId => _whatsAppClient.SendAudioByMediaIdAsync(Audio.PhoneNumber, mediaId, Audio.Voice, cancellationToken: cancellationToken));
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    public async Task<IActionResult> OnPostVideoAsync(CancellationToken cancellationToken)
    {
        SendResult = await UploadAndSendAsync(
            Video.File,
            cancellationToken,
            mediaId => _whatsAppClient.SendVideoByMediaIdAsync(Video.PhoneNumber, mediaId, Video.Caption, cancellationToken: cancellationToken));
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    public async Task<IActionResult> OnPostStickerAsync(CancellationToken cancellationToken)
    {
        SendResult = await UploadAndSendAsync(
            Sticker.File,
            cancellationToken,
            mediaId => _whatsAppClient.SendStickerByMediaIdAsync(Sticker.PhoneNumber, mediaId, cancellationToken: cancellationToken));
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    /// <summary>
    /// Sube <paramref name="file"/> a Meta (<see cref="IArjuyWhatsAppClient.UploadMediaAsync"/>) y,
    /// si la subida fue exitosa, ejecuta <paramref name="send"/> con el <c>media_id</c> resultante —
    /// mismo mecanismo de un solo paso que <c>WhatsAppMediaController.SendMedia</c> en Sample.Api.
    /// </summary>
    private async Task<MResult<string>> UploadAndSendAsync(
        IFormFile? file,
        CancellationToken cancellationToken,
        Func<string, Task<MResult<string>>> send)
    {
        if (file is null || file.Length == 0)
        {
            return MResult<string>.Fail("Elegí un archivo para adjuntar.");
        }

        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        var uploadResult = await _whatsAppClient.UploadMediaAsync(memoryStream.ToArray(), file.FileName, file.ContentType);
        if (!uploadResult.IsSuccess || uploadResult.Data is null)
        {
            return uploadResult.Error is { } error
                ? MResult<string>.Fail(error)
                : MResult<string>.Fail(uploadResult.Message ?? "No se pudo subir el archivo a Meta.");
        }

        return await send(uploadResult.Data);
    }

    public async Task<IActionResult> OnPostLocationAsync()
    {
        SendResult = await _whatsAppClient.SendLocationAsync(
            Location.PhoneNumber, Location.Latitude, Location.Longitude, Location.PlaceName, Location.Address);
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    public async Task<IActionResult> OnPostContactAsync()
    {
        var contact = new WhatsAppContact
        {
            Name = new WhatsAppContactName { FormattedName = Contact.ContactName },
        };

        if (!string.IsNullOrWhiteSpace(Contact.ContactPhone))
        {
            contact.Phones.Add(new WhatsAppContactPhone { Phone = Contact.ContactPhone, Type = "CELL" });
        }

        SendResult = await _whatsAppClient.SendContactsAsync(Contact.PhoneNumber, new[] { contact });
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    public async Task<IActionResult> OnPostReactionAsync()
    {
        SendResult = await _whatsAppClient.SendReactionAsync(Reaction.PhoneNumber, Reaction.MessageId, Reaction.Emoji);
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }

    public async Task<IActionResult> OnPostListTemplatesAsync()
    {
        TemplatesResult = await _whatsAppClient.GetMessageTemplatesAsync();
        ReceivedMessages = _messageStore.GetAll();
        return Page();
    }
}
