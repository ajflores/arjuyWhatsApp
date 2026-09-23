namespace ArjuyWhatsApp.Sample.Web;

/// <summary>
/// Handler de mensajes entrantes (mecanismo DI de <see cref="IWhatsAppMessageHandler"/>) que
/// guarda cada mensaje recibido en el <see cref="IMessageStore"/> en memoria, para que
/// <c>Pages/Index.cshtml</c> los pueda mostrar. Ver MANUAL.md sección 3.4.2 para el detalle del
/// mecanismo de handlers por DI.
/// </summary>
public class StoringMessageHandler : IWhatsAppMessageHandler
{
    private readonly IMessageStore _store;
    private readonly IArjuyWhatsAppClient _whatsAppClient;
    private readonly ILogger<StoringMessageHandler> _logger;

    public StoringMessageHandler(IMessageStore store, IArjuyWhatsAppClient whatsAppClient, ILogger<StoringMessageHandler> logger)
    {
        _store = store;
        _whatsAppClient = whatsAppClient;
        _logger = logger;
    }

    public async Task HandleAsync(WhatsAppMessageReceived message, CancellationToken cancellationToken = default)
    {
        _store.Add(message);

        _logger.LogInformation(
            "Mensaje recibido de {From} ({Type}, id {MessageId}): {Text}",
            message.From, message.Type, message.MessageId, message.Text);

        // Best-effort: marcamos el mensaje como leído (con indicador de "escribiendo...", ya que
        // en un chat real lo normal es que el vendedor vaya a responder) para que el remitente vea
        // el tilde azul en su WhatsApp. Si falla, solo se loguea — no tiene sentido romper la
        // recepción del mensaje por esto.
        var markAsReadResult = await _whatsAppClient.MarkAsReadWithTypingIndicatorAsync(message.MessageId, cancellationToken);
        if (!markAsReadResult.IsSuccess)
        {
            _logger.LogWarning(
                "No se pudo marcar como leído el mensaje {MessageId}: {Error}",
                message.MessageId, markAsReadResult.Message);
        }
    }
}
