namespace ArjuyWhatsApp.Sample.WinForms;

/// <summary>
/// Ventana principal del sample: envía mensajes de texto vía <see cref="IArjuyWhatsAppClient"/>
/// y muestra los mensajes entrantes que llegan por el webhook embebido (ver Program.cs).
/// </summary>
public partial class Form1 : Form
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;

    public Form1(IArjuyWhatsAppClient whatsAppClient)
    {
        _whatsAppClient = whatsAppClient;

        InitializeComponent();
    }

    private async void btnEnviar_Click(object sender, EventArgs e)
    {
        var phoneNumber = txtNumero.Text.Trim();
        var message = txtMensaje.Text.Trim();

        if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(message))
        {
            MessageBox.Show(this, "Completá número y mensaje.", "Faltan datos",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnEnviar.Enabled = false;
        lblResultado.Text = "Enviando...";

        try
        {
            // Este handler corre en el UI thread (es un event handler de WinForms disparado por
            // un click), así que acá SÍ es seguro tocar los controles directamente después del
            // await — a diferencia de OnMessageReceived más abajo, que llega desde un thread de
            // background del servidor HTTP embebido.
            var result = await _whatsAppClient.SendTextAsync(phoneNumber, message);

            lblResultado.Text = result.IsSuccess
                ? $"Enviado. Message id: {result.Data}"
                : $"Error: {result.Message}";
        }
        finally
        {
            btnEnviar.Enabled = true;
        }
    }

    /// <summary>
    /// Suscripto a <see cref="IArjuyWhatsAppClient.MessageReceived"/> desde Program.cs.
    ///
    /// ⚠️ IMPORTANTE: este evento llega en un thread de background del servidor HTTP embebido
    /// (Kestrel), NO en el UI thread de WinForms. Kestrel atiende cada request POST del webhook en
    /// un thread del thread pool; ProcessWebhookAsync dispara MessageReceived desde ESE mismo
    /// thread. Si acá adentro se tocara el ListBox directo, WinForms tira
    /// InvalidOperationException: "Cross-thread operation not valid: Control 'lstMensajes'
    /// accessed from a thread other than the thread it was created on" — cada Control de WinForms
    /// tiene afinidad con el thread en el que se creó (el UI thread), a diferencia de WPF donde el
    /// dueño es el Dispatcher pero el síntoma es análogo.
    ///
    /// La solución es marshalear la actualización al UI thread con this.Invoke(...) (síncrono,
    /// bloquea el thread de Kestrel hasta que la UI procesa el update) o this.BeginInvoke(...)
    /// (asíncrono, no bloquea al llamador). Acá se usa BeginInvoke porque no hay ningún valor de
    /// retorno que el thread de Kestrel necesite esperar — encolar el update y seguir.
    /// </summary>
    public void OnMessageReceived(object? sender, WhatsAppMessageReceivedEventArgs e)
    {
        var message = e.Message;
        var texto = $"{message.Timestamp:HH:mm:ss} | {message.From} | {message.Type} | {message.Text}";

        // ⚠️ Estamos en el thread de background de Kestrel: no tocar lstMensajes directo.
        if (IsHandleCreated)
        {
            BeginInvoke(() => lstMensajes.Items.Add(texto));
        }
    }
}
