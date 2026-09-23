namespace ArjuyWhatsApp.Sample.WinForms;

/// <summary>
/// Ventana principal del sample: envía mensajes de texto vía <see cref="IArjuyWhatsAppClient"/>
/// y muestra los mensajes entrantes que llegan por el webhook embebido (ver Program.cs).
/// </summary>
public partial class Form1 : Form
{
    private readonly IArjuyWhatsAppClient _whatsAppClient;

    /// <summary>
    /// Mapea el message id devuelto por Meta al enviar (ver <see cref="btnEnviar_Click"/>) con el
    /// índice del item correspondiente en <see cref="lstEnviados"/>, para poder actualizar ESE item
    /// in-place cuando llega un <see cref="IArjuyWhatsAppClient.MessageStatusUpdated"/> — ver
    /// <see cref="OnMessageStatusUpdated"/>. No hace falta lock: todo acceso a este diccionario ya
    /// ocurre marshaleado al UI thread (adentro de btnEnviar_Click, que corre en el UI thread por
    /// ser un event handler de click, o adentro del delegate de BeginInvoke en
    /// OnMessageStatusUpdated).
    /// </summary>
    private readonly Dictionary<string, int> _sentMessageListIndexByMessageId = new();

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

            // Trackeamos el message id -> índice en lstEnviados para poder pintar encima el estado
            // de entrega (✓/✓✓) cuando llegue el MessageStatusUpdated correspondiente más adelante.
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Data))
            {
                var index = lstEnviados.Items.Count;
                lstEnviados.Items.Add($"{DateTime.Now:HH:mm:ss} | {phoneNumber} | {message} | (enviando)");
                _sentMessageListIndexByMessageId[result.Data] = index;
            }
        }
        finally
        {
            btnEnviar.Enabled = true;
        }
    }

    /// <summary>
    /// Suscripto a <see cref="IArjuyWhatsAppClient.MessageStatusUpdated"/> desde Program.cs. Mismo
    /// problema de threading que <see cref="OnMessageReceived"/> (se dispara desde un thread de
    /// background de Kestrel, no el UI thread) — mismo remedio: marshalear con BeginInvoke.
    /// Actualiza in-place el item de <see cref="lstEnviados"/> que corresponde al mensaje, usando
    /// <see cref="_sentMessageListIndexByMessageId"/>. Si el status llega para un mensaje que esta
    /// instancia no mandó (ej. se reinició la app), simplemente no hace nada — no hay item que
    /// actualizar.
    /// </summary>
    public void OnMessageStatusUpdated(object? sender, WhatsAppMessageStatusUpdateEventArgs e)
    {
        var status = e.StatusUpdate;
        var estadoTexto = status.Status switch
        {
            WhatsAppMessageStatus.Sent => "✓ enviado",
            WhatsAppMessageStatus.Delivered => "✓✓ entregado",
            WhatsAppMessageStatus.Read => "✓✓ leído",
            WhatsAppMessageStatus.Failed => $"✗ falló ({status.ErrorMessage ?? status.ErrorCode?.ToString() ?? "error desconocido"})",
            _ => status.Status.ToString()
        };

        if (IsHandleCreated)
        {
            BeginInvoke(() =>
            {
                if (!_sentMessageListIndexByMessageId.TryGetValue(status.MessageId, out var index))
                {
                    return;
                }

                if (index < 0 || index >= lstEnviados.Items.Count)
                {
                    return;
                }

                // Reemplazar el item completo (no solo el sufijo) porque ListBox no soporta
                // editar una porción del texto de un item — hay que reasignar el string entero.
                var textoActual = lstEnviados.Items[index]?.ToString() ?? string.Empty;
                var separadorIndex = textoActual.LastIndexOf(" | (", StringComparison.Ordinal);
                var base_ = separadorIndex >= 0 ? textoActual[..separadorIndex] : textoActual;
                lstEnviados.Items[index] = $"{base_} | ({estadoTexto})";
            });
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

        // Marcamos el mensaje como leído best-effort: no bloqueamos ni rompemos el flujo de
        // recepción si esto falla (ej. token vencido, o el message id ya venció los 30 días que
        // documenta Meta como límite para marcar como leído — no debería pasar acá porque es el
        // mensaje recién recibido, pero MarkAsReadAsync ya devuelve MResult.Fail en vez de tirar
        // excepción, así que esto nunca puede romper OnMessageReceived).
        _ = _whatsAppClient.MarkAsReadAsync(message.MessageId);
    }
}
