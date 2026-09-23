using Microsoft.AspNetCore.Mvc;
using ArjuyWhatsApp;

namespace ArjuyWhatsApp.Sample.Api.Controllers;

/// <summary>Endpoint para listar las plantillas de mensaje de la cuenta de WhatsApp Business configurada.</summary>
[ApiController]
[Route("api/templates")]
public class WhatsAppTemplatesController : ControllerBase
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;

    public WhatsAppTemplatesController(IArjuyWhatsAppClient whatsAppClient)
    {
        _whatsAppClient = whatsAppClient;
    }

    [HttpGet]
    public async Task<ActionResult<MResult<IReadOnlyList<WhatsAppMessageTemplate>>>> GetTemplates(CancellationToken cancellationToken)
    {
        // 🔴 PONÉ UN BREAKPOINT ACÁ para ver la lista completa de plantillas (ya paginada) devuelta por Meta
        var result = await _whatsAppClient.GetMessageTemplatesAsync(cancellationToken);
        return Ok(result);
    }
}
