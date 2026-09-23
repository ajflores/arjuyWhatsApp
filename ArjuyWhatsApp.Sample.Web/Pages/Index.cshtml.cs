using ArjuyWhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArjuyWhatsApp.Sample.Web.Pages;

/// <summary>
/// Página única de la sample: formulario para mandar un texto (POST, patrón PRG) y listado de
/// los mensajes recibidos hasta el momento, leídos desde <see cref="IMessageStore"/>.
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

    /// <summary>Resultado del último envío (éxito o error), para mostrar en la misma página tras el POST.</summary>
    public MResult<string>? SendResult { get; private set; }

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
}
