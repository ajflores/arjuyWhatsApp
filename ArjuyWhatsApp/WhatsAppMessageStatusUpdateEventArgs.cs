namespace ArjuyWhatsApp;

/// <summary>Argumentos del evento <see cref="IArjuyWhatsAppClient.MessageStatusUpdated"/>.</summary>
public class WhatsAppMessageStatusUpdateEventArgs : EventArgs
{
    /// <summary>Actualización de estado de entrega ya parseada desde el webhook de Meta.</summary>
    public WhatsAppMessageStatusUpdate StatusUpdate { get; }

    /// <summary>Crea los argumentos del evento con la actualización de estado recibida.</summary>
    public WhatsAppMessageStatusUpdateEventArgs(WhatsAppMessageStatusUpdate statusUpdate)
    {
        StatusUpdate = statusUpdate;
    }
}
