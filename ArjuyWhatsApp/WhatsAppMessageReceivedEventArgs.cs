namespace ArjuyWhatsApp;

/// <summary>Argumentos del evento <see cref="IArjuyWhatsAppClient.MessageReceived"/>.</summary>
public class WhatsAppMessageReceivedEventArgs : EventArgs
{
    /// <summary>Mensaje entrante ya parseado desde el webhook de Meta.</summary>
    public WhatsAppMessageReceived Message { get; }

    /// <summary>Crea los argumentos del evento con el mensaje recibido.</summary>
    public WhatsAppMessageReceivedEventArgs(WhatsAppMessageReceived message)
    {
        Message = message;
    }
}
