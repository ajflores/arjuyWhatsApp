using System.Collections.ObjectModel;
using System.Windows;

namespace ArjuyWhatsApp.Sample.Wpf;

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
    }
}
