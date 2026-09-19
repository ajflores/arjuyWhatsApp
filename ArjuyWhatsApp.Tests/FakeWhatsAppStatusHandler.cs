namespace ArjuyWhatsApp.Tests;

/// <summary>
/// <see cref="IWhatsAppStatusHandler"/> de prueba que registra cada estado recibido en una
/// lista compartida (inyectada como singleton en el test), para poder verificar que
/// <c>ProcessWebhookAsync</c> efectivamente resolvió e invocó handlers registrados por DI.
/// </summary>
internal class FakeWhatsAppStatusHandler : IWhatsAppStatusHandler
{
    private readonly List<WhatsAppMessageStatusUpdate> _received;

    public FakeWhatsAppStatusHandler(List<WhatsAppMessageStatusUpdate> received)
    {
        _received = received;
    }

    public Task HandleAsync(WhatsAppMessageStatusUpdate status, CancellationToken cancellationToken = default)
    {
        _received.Add(status);
        return Task.CompletedTask;
    }
}

/// <summary>
/// <see cref="IWhatsAppStatusHandler"/> de prueba que siempre explota, para verificar que una
/// excepción de un handler no interrumpe a los demás ni el procesamiento del webhook.
/// </summary>
internal class ThrowingWhatsAppStatusHandler : IWhatsAppStatusHandler
{
    public Task HandleAsync(WhatsAppMessageStatusUpdate status, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Boom — handler de prueba que siempre falla.");
    }
}
