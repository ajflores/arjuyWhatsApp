namespace ArjuyWhatsApp.Tests;

/// <summary>
/// <see cref="IWhatsAppMessageHandler"/> de prueba que registra cada mensaje recibido en una
/// lista compartida (inyectada como singleton en el test), para poder verificar que
/// <c>ProcessWebhookAsync</c> efectivamente resolvió e invocó handlers registrados por DI.
/// </summary>
internal class FakeWhatsAppMessageHandler : IWhatsAppMessageHandler
{
    private readonly List<WhatsAppMessageReceived> _received;

    public FakeWhatsAppMessageHandler(List<WhatsAppMessageReceived> received)
    {
        _received = received;
    }

    public Task HandleAsync(WhatsAppMessageReceived message, CancellationToken cancellationToken = default)
    {
        _received.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>
/// <see cref="IWhatsAppMessageHandler"/> de prueba que siempre explota, para verificar que una
/// excepción de un handler no interrumpe a los demás ni el procesamiento del webhook.
/// </summary>
internal class ThrowingWhatsAppMessageHandler : IWhatsAppMessageHandler
{
    public Task HandleAsync(WhatsAppMessageReceived message, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Boom — handler de prueba que siempre falla.");
    }
}
