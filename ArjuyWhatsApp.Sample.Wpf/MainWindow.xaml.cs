using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ArjuyWhatsApp.Sample.Wpf;

/// <summary>
/// Estado de entrega de un mensaje que ESTE sample mandó, trackeado por <see cref="MessageId"/>
/// para poder actualizarlo cuando llega un evento <see cref="IArjuyWhatsAppClient.MessageStatusUpdated"/>.
/// Implementa <see cref="INotifyPropertyChanged"/> porque, a diferencia de un <c>Add</c> a la
/// <see cref="ObservableCollection{T}"/> (que WPF sí detecta solo), actualizar <see cref="Status"/>
/// de un item YA agregado no dispara notificación de UI por sí mismo — sin esto, el ✓✓ nunca se
/// actualizaría en pantalla aunque el dato cambie por dentro.
/// </summary>
public class SentMessageStatus : INotifyPropertyChanged
{
    private string _status = "✓ enviado";

    public string MessageId { get; }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }

    public SentMessageStatus(string messageId)
    {
        MessageId = messageId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Ventana principal del sample: envía mensajes de texto vía <see cref="IArjuyWhatsAppClient"/>
/// y muestra los mensajes entrantes que llegan por el webhook embebido (ver App.xaml.cs).
/// </summary>
public partial class MainWindow : Window
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;

    /// <summary>
    /// Colección bindeada al ListView del XAML (ver MainWindow.xaml, ItemsSource="{Binding
    /// MensajesRecibidos}"). ObservableCollection notifica a la UI de cada Add automáticamente —
    /// por eso el DataContext de la ventana se setea a "this" en el constructor.
    /// </summary>
    public ObservableCollection<WhatsAppMessageReceived> MensajesRecibidos { get; } = new();

    /// <summary>
    /// Mensajes que ESTE sample mandó, con su estado de entrega (✓ enviado / ✓✓ entregado / ✓✓
    /// leído / ✗ falló) actualizado en vivo desde <see cref="OnMessageStatusUpdated"/>.
    /// </summary>
    public ObservableCollection<SentMessageStatus> MensajesEnviados { get; } = new();

    public MainWindow(IArjuyWhatsAppClient whatsAppClient)
    {
        _whatsAppClient = whatsAppClient;

        InitializeComponent();

        DataContext = this;
    }

    private async void btnEnviar_Click(object sender, RoutedEventArgs e)
    {
        var phoneNumber = txtNumero.Text.Trim();
        var message = txtMensaje.Text.Trim();

        if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(message))
        {
            MessageBox.Show(this, "Completá número y mensaje.", "Faltan datos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        btnEnviar.IsEnabled = false;
        lblResultado.Text = "Enviando...";

        try
        {
            // Este handler corre en el Dispatcher thread (es un event handler de WPF disparado
            // por un click), así que acá SÍ es seguro tocar los controles directamente después
            // del await — a diferencia de OnMessageReceived más abajo, que llega desde un thread
            // de background del servidor HTTP embebido.
            var result = await _whatsAppClient.SendTextAsync(phoneNumber, message);

            lblResultado.Text = result.IsSuccess
                ? $"Enviado. Message id: {result.Data}"
                : $"Error: {result.Message}";

            if (result.IsSuccess && result.Data is not null)
            {
                // Trackeamos el mensaje recién enviado por su MessageId para poder pintarle
                // el estado de entrega cuando llegue el evento MessageStatusUpdated más abajo.
                MensajesEnviados.Add(new SentMessageStatus(result.Data));
            }
        }
        finally
        {
            btnEnviar.IsEnabled = true;
        }
    }

    /// <summary>
    /// Suscripto a <see cref="IArjuyWhatsAppClient.MessageReceived"/> desde App.xaml.cs.
    ///
    /// ⚠️ IMPORTANTE: este evento llega en un thread de background del servidor HTTP embebido
    /// (Kestrel), NO en el Dispatcher thread de WPF. Kestrel atiende cada request POST del webhook
    /// en un thread del thread pool; ProcessWebhookAsync dispara MessageReceived desde ESE mismo
    /// thread. Si acá adentro se tocara MensajesRecibidos (la ObservableCollection bindeada) de
    /// forma directa, WPF tira InvalidOperationException: "The calling thread cannot access this
    /// object because a different thread owns it" (el equivalente WPF del cross-thread de
    /// WinForms — acá el dueño es el Dispatcher, no un Control puntual).
    ///
    /// La solución es marshalear la actualización al Dispatcher thread con
    /// Application.Current.Dispatcher.Invoke(...) (síncrono, bloquea el thread de Kestrel hasta
    /// que la UI procesa el update) o Dispatcher.InvokeAsync(...) (asíncrono, no bloquea al
    /// llamador). Acá se usa InvokeAsync porque no hay ningún valor de retorno que el thread de
    /// Kestrel necesite esperar — encolar el update y seguir.
    /// </summary>
    public void OnMessageReceived(object? sender, WhatsAppMessageReceivedEventArgs e)
    {
        var message = e.Message;

        // ⚠️ Estamos en el thread de background de Kestrel: no tocar MensajesRecibidos directo.
        Application.Current.Dispatcher.InvokeAsync(() => MensajesRecibidos.Add(message));

        // Best-effort: marcamos el mensaje como leído en Meta (✓✓ azul del lado del remitente).
        // "Fire and forget" a propósito — este handler no es async y no hay nada más que hacer
        // acá si falla (Meta lo tolera, no es crítico para el flujo de recepción).
        _ = MarkAsReadBestEffortAsync(message.MessageId);
    }

    private async Task MarkAsReadBestEffortAsync(string messageId)
    {
        try
        {
            await _whatsAppClient.MarkAsReadAsync(messageId);
        }
        catch
        {
            // Best-effort: un fallo acá (red, token vencido, etc.) no debe tirar abajo la
            // recepción del mensaje, que ya se mostró en MensajesRecibidos igual.
        }
    }

    /// <summary>
    /// Suscripto a <see cref="IArjuyWhatsAppClient.MessageStatusUpdated"/> desde App.xaml.cs.
    /// Mismo problema de threading que <see cref="OnMessageReceived"/> — este evento también se
    /// dispara desde el thread de background de Kestrel, así que marshaleamos con
    /// Dispatcher.InvokeAsync antes de tocar <see cref="MensajesEnviados"/>.
    /// </summary>
    public void OnMessageStatusUpdated(object? sender, WhatsAppMessageStatusUpdateEventArgs e)
    {
        var statusUpdate = e.StatusUpdate;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var tracked = MensajesEnviados.FirstOrDefault(m => m.MessageId == statusUpdate.MessageId);
            if (tracked is null)
            {
                // Puede pasar si la ventana se reinició entre el envío y este evento, o si el
                // mensaje fue mandado por otro proceso/sesión — no es un error, simplemente no
                // hay nada que actualizar en esta instancia de la UI.
                return;
            }

            tracked.Status = statusUpdate.Status switch
            {
                WhatsAppMessageStatus.Sent => "✓ enviado",
                WhatsAppMessageStatus.Delivered => "✓✓ entregado",
                WhatsAppMessageStatus.Read => "✓✓ leído",
                WhatsAppMessageStatus.Failed => $"✗ falló ({statusUpdate.ErrorMessage})",
                _ => tracked.Status
            };
        });
    }
}
