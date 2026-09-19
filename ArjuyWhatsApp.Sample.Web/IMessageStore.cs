namespace ArjuyWhatsApp.Sample.Web;

/// <summary>
/// Almacén en memoria (proceso único, no persistente) de los mensajes de WhatsApp recibidos, para
/// que <c>Pages/Index.cshtml</c> los pueda listar. Pensado solo para esta sample — en una app real
/// esto sería una tabla en base de datos, resuelta desde un <see cref="IWhatsAppMessageHandler"/>
/// Scoped (ver <see cref="StoringMessageHandler"/>).
/// </summary>
public interface IMessageStore
{
    /// <summary>Agrega un mensaje recibido al almacén.</summary>
    void Add(WhatsAppMessageReceived message);

    /// <summary>Devuelve todos los mensajes recibidos hasta ahora, más nuevo primero.</summary>
    IReadOnlyList<WhatsAppMessageReceived> GetAll();
}

/// <summary>
/// Implementación simple de <see cref="IMessageStore"/> con una <see cref="List{T}"/> protegida
/// por <c>lock</c> — suficiente para una sample de un solo proceso; no pensada para escenarios de
/// alta concurrencia ni para sobrevivir un restart de la app.
/// </summary>
public class InMemoryMessageStore : IMessageStore
{
    private readonly List<WhatsAppMessageReceived> _messages = new();
    private readonly object _lock = new();

    public void Add(WhatsAppMessageReceived message)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }
    }

    public IReadOnlyList<WhatsAppMessageReceived> GetAll()
    {
        lock (_lock)
        {
            // Copia defensiva: devolvemos un snapshot ordenado más nuevo primero, sin exponer la
            // lista interna (que sigue mutando por detrás con cada mensaje entrante).
            return _messages
                .OrderByDescending(m => m.Timestamp)
                .ToList();
        }
    }
}
