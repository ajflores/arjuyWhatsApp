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

    /// <summary>Resultado del último envío (éxito o error), para mostrar en la misma página tras el POST.</summary>
    public MResult<string>? SendResult { get; private set; }

    /// <summary>Mensajes recibidos hasta ahora, más nuevo primero.</summary>
    public IReadOnlyList<WhatsAppMessageReceived> ReceivedMessages { get; private set; } = Array.Empty<WhatsAppMessageReceived>();

    public class SendTextInput
    {
        public string PhoneNumber { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
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
}
