using Microsoft.AspNetCore.Mvc;
using ArjuyWhatsApp;

namespace ArjuyWhatsApp.Sample.Api.Controllers;

/// <summary>
/// Endpoints simples para probar el ENVÍO de mensajes desde Postman/navegador/curl, sin tener
/// que levantar la consola (ArjuyWhatsApp.Sample) aparte. Cada endpoint devuelve el
/// <see cref="MResult{T}"/> tal cual lo entrega la librería, serializado como JSON.
/// </summary>
[ApiController]
[Route("api/send")]
public class WhatsAppSendController : ControllerBase
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;

    public WhatsAppSendController(IArjuyWhatsAppClient whatsAppClient)
    {
        _whatsAppClient = whatsAppClient;
    }

    /// <summary>Body de <see cref="SendText"/>.</summary>
    public record SendTextRequest(string PhoneNumber, string Message);

    [HttpPost("text")]
    public async Task<ActionResult<MResult<string>>> SendText([FromBody] SendTextRequest request)
    {
        // 🔴 PONÉ UN BREAKPOINT ACÁ para ver el request antes de mandarlo, y en la línea de abajo para ver el MResult de la respuesta
        var result = await _whatsAppClient.SendTextAsync(request.PhoneNumber, request.Message);
        return Ok(result);
    }

    /// <summary>Body de <see cref="SendTemplate"/>.</summary>
    public record SendTemplateRequest(string PhoneNumber, string TemplateName, string LanguageCode, List<string>? Parameters);

    [HttpPost("template")]
    public async Task<ActionResult<MResult<string>>> SendTemplate([FromBody] SendTemplateRequest request)
    {
        // 🔴 PONÉ UN BREAKPOINT ACÁ para ver el request antes de mandarlo, y en la línea de abajo para ver el MResult de la respuesta
        var result = await _whatsAppClient.SendTemplateAsync(
            request.PhoneNumber,
            request.TemplateName,
            request.LanguageCode,
            request.Parameters ?? new List<string>());

        return Ok(result);
    }

    /// <summary>Body de <see cref="SendImage"/>.</summary>
    public record SendImageRequest(string PhoneNumber, string ImageUrl, string? Caption);

    [HttpPost("image")]
    public async Task<ActionResult<MResult<string>>> SendImage([FromBody] SendImageRequest request)
    {
        // 🔴 PONÉ UN BREAKPOINT ACÁ para ver el request antes de mandarlo, y en la línea de abajo para ver el MResult de la respuesta
        var result = await _whatsAppClient.SendImageAsync(request.PhoneNumber, request.ImageUrl, request.Caption);
        return Ok(result);
    }

    /// <summary>Body de <see cref="SendDocument"/>.</summary>
    public record SendDocumentRequest(string PhoneNumber, string DocumentUrl, string FileName, string? Caption);

    [HttpPost("document")]
    public async Task<ActionResult<MResult<string>>> SendDocument([FromBody] SendDocumentRequest request)
    {
        // 🔴 PONÉ UN BREAKPOINT ACÁ para ver el request antes de mandarlo, y en la línea de abajo para ver el MResult de la respuesta
        var result = await _whatsAppClient.SendDocumentAsync(
            request.PhoneNumber,
            request.DocumentUrl,
            request.FileName,
            request.Caption);

        return Ok(result);
    }
}
