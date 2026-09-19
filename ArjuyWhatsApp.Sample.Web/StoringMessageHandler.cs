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
    private readonly ILogger<StoringMessageHandler> _logger;

    public StoringMessageHandler(IMessageStore store, ILogger<StoringMessageHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task HandleAsync(WhatsAppMessageReceived message, CancellationToken cancellationToken = default)
    {
        _store.Add(message);

        _logger.LogInformation(
            "Mensaje recibido de {From} ({Type}, id {MessageId}): {Text}",
            message.From, message.Type, message.MessageId, message.Text);

        return Task.CompletedTask;
    }
}
