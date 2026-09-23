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

    /// <summary>Body de <see cref="SendAudio"/>.</summary>
    public record SendAudioRequest(string PhoneNumber, string AudioUrl, bool Voice = false);

    [HttpPost("audio")]
    public async Task<ActionResult<MResult<string>>> SendAudio([FromBody] SendAudioRequest request, CancellationToken cancellationToken)
    {
        var result = await _whatsAppClient.SendAudioAsync(request.PhoneNumber, request.AudioUrl, request.Voice, cancellationToken: cancellationToken);
        return Ok(result);
    }

    /// <summary>Body de <see cref="SendVideo"/>.</summary>
    public record SendVideoRequest(string PhoneNumber, string VideoUrl, string? Caption);

    [HttpPost("video")]
    public async Task<ActionResult<MResult<string>>> SendVideo([FromBody] SendVideoRequest request, CancellationToken cancellationToken)
    {
        var result = await _whatsAppClient.SendVideoAsync(request.PhoneNumber, request.VideoUrl, request.Caption, cancellationToken: cancellationToken);
        return Ok(result);
    }

    /// <summary>Body de <see cref="SendSticker"/>.</summary>
    public record SendStickerRequest(string PhoneNumber, string StickerUrl);

    [HttpPost("sticker")]
    public async Task<ActionResult<MResult<string>>> SendSticker([FromBody] SendStickerRequest request, CancellationToken cancellationToken)
    {
        var result = await _whatsAppClient.SendStickerAsync(request.PhoneNumber, request.StickerUrl, cancellationToken: cancellationToken);
        return Ok(result);
    }

    /// <summary>Body de <see cref="SendLocation"/>.</summary>
    public record SendLocationRequest(string PhoneNumber, double Latitude, double Longitude, string? Name, string? Address);

    [HttpPost("location")]
    public async Task<ActionResult<MResult<string>>> SendLocation([FromBody] SendLocationRequest request, CancellationToken cancellationToken)
    {
        var result = await _whatsAppClient.SendLocationAsync(
            request.PhoneNumber, request.Latitude, request.Longitude, request.Name, request.Address,
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    /// <summary>Body de <see cref="SendContact"/> — un solo contacto por simplicidad del sample (la librería admite varios en <see cref="IArjuyWhatsAppClient.SendContactsAsync"/>).</summary>
    public record SendContactRequest(string PhoneNumber, WhatsAppContact Contact);

    [HttpPost("contact")]
    public async Task<ActionResult<MResult<string>>> SendContact([FromBody] SendContactRequest request, CancellationToken cancellationToken)
    {
        var result = await _whatsAppClient.SendContactsAsync(request.PhoneNumber, [request.Contact], cancellationToken: cancellationToken);
        return Ok(result);
    }

    /// <summary>Body de <see cref="SendReaction"/>.</summary>
    public record SendReactionRequest(string PhoneNumber, string MessageId, string Emoji);

    [HttpPost("reaction")]
    public async Task<ActionResult<MResult<string>>> SendReaction([FromBody] SendReactionRequest request, CancellationToken cancellationToken)
    {
        // Emoji = "" remueve una reacción puesta antes — no es un error, es el mecanismo oficial de Meta.
        var result = await _whatsAppClient.SendReactionAsync(request.PhoneNumber, request.MessageId, request.Emoji, cancellationToken);
        return Ok(result);
    }

    /// <summary>Body de <see cref="SendInteractiveButtons"/>.</summary>
    public record InteractiveButtonRequest(string Id, string Title);
    public record SendInteractiveButtonsRequest(string PhoneNumber, string BodyText, List<InteractiveButtonRequest> Buttons);

    [HttpPost("interactive-buttons")]
    public async Task<ActionResult<MResult<string>>> SendInteractiveButtons([FromBody] SendInteractiveButtonsRequest request)
    {
        var buttons = request.Buttons.Select(b => (b.Id, b.Title));
        var result = await _whatsAppClient.SendInteractiveButtonsAsync(request.PhoneNumber, request.BodyText, buttons);
        return Ok(result);
    }

    /// <summary>Body de <see cref="SendInteractiveList"/>.</summary>
    public record InteractiveListRowRequest(string Id, string Title, string? Description);
    public record InteractiveListSectionRequest(string SectionTitle, List<InteractiveListRowRequest> Rows);
    public record SendInteractiveListRequest(string PhoneNumber, string BodyText, string ButtonText, List<InteractiveListSectionRequest> Sections);

    [HttpPost("interactive-list")]
    public async Task<ActionResult<MResult<string>>> SendInteractiveList([FromBody] SendInteractiveListRequest request)
    {
        var sections = request.Sections.Select(s =>
            (s.SectionTitle, (IEnumerable<(string Id, string Title, string? Description)>)s.Rows
                .Select(r => (r.Id, r.Title, r.Description)).ToList()));

        var result = await _whatsAppClient.SendInteractiveListAsync(request.PhoneNumber, request.BodyText, request.ButtonText, sections);
        return Ok(result);
    }
}
