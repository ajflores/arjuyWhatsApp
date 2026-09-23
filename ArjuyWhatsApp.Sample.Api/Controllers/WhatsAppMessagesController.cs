using Microsoft.AspNetCore.Mvc;
using ArjuyWhatsApp;

namespace ArjuyWhatsApp.Sample.Api.Controllers;

/// <summary>Endpoints para marcar mensajes entrantes como leídos, con o sin indicador de "escribiendo...".</summary>
[ApiController]
[Route("api/messages")]
public class WhatsAppMessagesController : ControllerBase
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;

    public WhatsAppMessagesController(IArjuyWhatsAppClient whatsAppClient)
    {
        _whatsAppClient = whatsAppClient;
    }

    [HttpPost("{messageId}/mark-read")]
    public async Task<ActionResult<MResult<bool>>> MarkAsRead(string messageId, CancellationToken cancellationToken)
    {
        var result = await _whatsAppClient.MarkAsReadAsync(messageId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{messageId}/mark-read-typing")]
    public async Task<ActionResult<MResult<bool>>> MarkAsReadWithTypingIndicator(string messageId, CancellationToken cancellationToken)
    {
        // 🔴 PONÉ UN BREAKPOINT ACÁ — usá esto solo si vas a responder a continuación (ver XML doc del método)
        var result = await _whatsAppClient.MarkAsReadWithTypingIndicatorAsync(messageId, cancellationToken);
        return Ok(result);
    }
}
