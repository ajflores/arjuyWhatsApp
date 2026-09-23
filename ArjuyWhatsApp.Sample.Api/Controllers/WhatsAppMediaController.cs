using Microsoft.AspNetCore.Mvc;
using ArjuyWhatsApp;

namespace ArjuyWhatsApp.Sample.Api.Controllers;

/// <summary>
/// Endpoints para subir un archivo local a Meta y mandarlo por <c>media_id</c> (sin URL pública),
/// y para descargar media recibida en un webhook. Complementa a <see cref="WhatsAppSendController"/>,
/// que solo cubre envío por URL pública.
/// </summary>
[ApiController]
[Route("api")]
public class WhatsAppMediaController : ControllerBase
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;

    public WhatsAppMediaController(IArjuyWhatsAppClient whatsAppClient)
    {
        _whatsAppClient = whatsAppClient;
    }

    /// <summary>Tipo de media a mandar por <c>media_id</c> en <see cref="SendMedia"/>.</summary>
    public enum SendMediaType
    {
        Image,
        Document,
        Audio,
        Video,
        Sticker
    }

    /// <summary>
    /// Sube el archivo adjunto a Meta (<see cref="IArjuyWhatsAppClient.UploadMediaAsync"/>) y lo manda
    /// en el mismo request por <c>media_id</c> — un solo paso para el caso común de "tengo un archivo
    /// local (ej. un PDF de reserva generado al vuelo) y quiero mandarlo sin publicarlo antes en una
    /// URL pública".
    /// </summary>
    [HttpPost("send/media")]
    public async Task<ActionResult<MResult<string>>> SendMedia(
        [FromForm] string phoneNumber,
        [FromForm] SendMediaType type,
        [FromForm] IFormFile file,
        [FromForm] string? caption,
        [FromForm] string? fileName,
        CancellationToken cancellationToken)
    {
        // 🔴 PONÉ UN BREAKPOINT ACÁ para ver el archivo recibido antes de subirlo a Meta
        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        var uploadResult = await _whatsAppClient.UploadMediaAsync(memoryStream.ToArray(), file.FileName, file.ContentType);
        if (!uploadResult.IsSuccess || uploadResult.Data is null)
        {
            MResult<string> failResult = uploadResult.Error is { } error
                ? MResult<string>.Fail(error)
                : MResult<string>.Fail(uploadResult.Message ?? "No se pudo subir el archivo a Meta.");

            return Ok(failResult);
        }

        var mediaId = uploadResult.Data;

        // 🔴 PONÉ UN BREAKPOINT ACÁ para ver el media_id devuelto por Meta antes de mandarlo
        var sendResult = type switch
        {
            SendMediaType.Image => await _whatsAppClient.SendImageByMediaIdAsync(phoneNumber, mediaId, caption),
            SendMediaType.Document => await _whatsAppClient.SendDocumentByMediaIdAsync(phoneNumber, mediaId, fileName ?? file.FileName, caption),
            SendMediaType.Audio => await _whatsAppClient.SendAudioByMediaIdAsync(phoneNumber, mediaId, cancellationToken: cancellationToken),
            SendMediaType.Video => await _whatsAppClient.SendVideoByMediaIdAsync(phoneNumber, mediaId, caption, cancellationToken: cancellationToken),
            SendMediaType.Sticker => await _whatsAppClient.SendStickerByMediaIdAsync(phoneNumber, mediaId, cancellationToken: cancellationToken),
            _ => MResult<string>.Fail($"Tipo de media no soportado: {type}")
        };

        return Ok(sendResult);
    }

    /// <summary>
    /// Descarga el contenido binario de un archivo de media (ej. recibido en un webhook entrante) y lo
    /// devuelve tal cual en la respuesta HTTP.
    /// </summary>
    [HttpGet("media/{mediaId}")]
    public async Task<IActionResult> DownloadMedia(string mediaId)
    {
        // 🔴 PONÉ UN BREAKPOINT ACÁ para inspeccionar el resultado de la descarga
        var result = await _whatsAppClient.DownloadMediaAsync(mediaId);

        if (!result.IsSuccess || result.Data is null)
        {
            return NotFound(result);
        }

        return File(result.Data, "application/octet-stream", mediaId);
    }
}
