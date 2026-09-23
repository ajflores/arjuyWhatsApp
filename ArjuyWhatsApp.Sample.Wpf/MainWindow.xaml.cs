using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;

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

    // -----------------------------------------------------------------------------------------
    // Pestañas de funcionalidades (imagen, documento, audio, video, sticker, ubicación, contacto,
    // reacción, plantillas). Cada ruta de archivo elegida se guarda en un campo propio (no en el
    // Tag de un control, a diferencia de la sample WinForms) porque acá cada control ya tiene su
    // x:Name fijo declarado en el XAML — no hace falta un diccionario genérico por pestaña.
    // -----------------------------------------------------------------------------------------

    private string? _imagenArchivoPath;
    private string? _documentoArchivoPath;
    private string? _audioArchivoPath;
    private string? _videoArchivoPath;
    private string? _stickerArchivoPath;

    private static string? ElegirArchivo()
    {
        var dialog = new OpenFileDialog();
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string FormatResult(MResult<string> result)
    {
        return result.IsSuccess
            ? $"Enviado. Message id: {result.Data}"
            : $"Error: {result.Message}";
    }

    private static string GuessMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Sube <paramref name="filePath"/> a Meta y, si la subida fue exitosa, ejecuta
    /// <paramref name="send"/> con el número destino y el <c>media_id</c> resultante — mismo
    /// mecanismo de un solo paso que <c>WhatsAppMediaController.SendMedia</c> en Sample.Api.
    /// </summary>
    private async Task<string> UploadAndSendAsync(string phoneNumber, string? filePath, Func<string, string, Task<MResult<string>>> send)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(filePath))
        {
            return "Completá el número y elegí un archivo.";
        }

        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var fileName = Path.GetFileName(filePath);

        var uploadResult = await _whatsAppClient.UploadMediaAsync(fileBytes, fileName, GuessMimeType(fileName));
        if (!uploadResult.IsSuccess || uploadResult.Data is null)
        {
            return $"Error al subir: {uploadResult.Message}";
        }

        var sendResult = await send(phoneNumber, uploadResult.Data);
        return FormatResult(sendResult);
    }

    private void BtnImagenElegirArchivo_Click(object sender, RoutedEventArgs e)
    {
        var path = ElegirArchivo();
        if (path is null)
        {
            return;
        }

        _imagenArchivoPath = path;
        lblImagenArchivo.Text = Path.GetFileName(path);
    }

    private async void BtnImagenEnviar_Click(object sender, RoutedEventArgs e)
    {
        txtImagenResultado.Text = "Subiendo y enviando...";
        txtImagenResultado.Text = await UploadAndSendAsync(
            txtImagenNumero.Text.Trim(), _imagenArchivoPath,
            (phoneNumber, mediaId) => _whatsAppClient.SendImageByMediaIdAsync(phoneNumber, mediaId, EmptyToNull(txtImagenCaption.Text)));
    }

    private void BtnDocumentoElegirArchivo_Click(object sender, RoutedEventArgs e)
    {
        var path = ElegirArchivo();
        if (path is null)
        {
            return;
        }

        _documentoArchivoPath = path;
        lblDocumentoArchivo.Text = Path.GetFileName(path);
    }

    private async void BtnDocumentoEnviar_Click(object sender, RoutedEventArgs e)
    {
        txtDocumentoResultado.Text = "Subiendo y enviando...";
        txtDocumentoResultado.Text = await UploadAndSendAsync(
            txtDocumentoNumero.Text.Trim(), _documentoArchivoPath,
            (phoneNumber, mediaId) =>
            {
                var nombreMostrado = txtDocumentoNombreMostrado.Text.Trim();
                var fileName = string.IsNullOrWhiteSpace(nombreMostrado)
                    ? Path.GetFileName(_documentoArchivoPath ?? string.Empty)
                    : nombreMostrado;
                return _whatsAppClient.SendDocumentByMediaIdAsync(phoneNumber, mediaId, fileName, EmptyToNull(txtDocumentoCaption.Text));
            });
    }

    private void BtnAudioElegirArchivo_Click(object sender, RoutedEventArgs e)
    {
        var path = ElegirArchivo();
        if (path is null)
        {
            return;
        }

        _audioArchivoPath = path;
        lblAudioArchivo.Text = Path.GetFileName(path);
    }

    private async void BtnAudioEnviar_Click(object sender, RoutedEventArgs e)
    {
        txtAudioResultado.Text = "Subiendo y enviando...";
        txtAudioResultado.Text = await UploadAndSendAsync(
            txtAudioNumero.Text.Trim(), _audioArchivoPath,
            (phoneNumber, mediaId) => _whatsAppClient.SendAudioByMediaIdAsync(phoneNumber, mediaId, chkAudioVoz.IsChecked == true));
    }

    private void BtnVideoElegirArchivo_Click(object sender, RoutedEventArgs e)
    {
        var path = ElegirArchivo();
        if (path is null)
        {
            return;
        }

        _videoArchivoPath = path;
        lblVideoArchivo.Text = Path.GetFileName(path);
    }

    private async void BtnVideoEnviar_Click(object sender, RoutedEventArgs e)
    {
        txtVideoResultado.Text = "Subiendo y enviando...";
        txtVideoResultado.Text = await UploadAndSendAsync(
            txtVideoNumero.Text.Trim(), _videoArchivoPath,
            (phoneNumber, mediaId) => _whatsAppClient.SendVideoByMediaIdAsync(phoneNumber, mediaId, EmptyToNull(txtVideoCaption.Text)));
    }

    private void BtnStickerElegirArchivo_Click(object sender, RoutedEventArgs e)
    {
        var path = ElegirArchivo();
        if (path is null)
        {
            return;
        }

        _stickerArchivoPath = path;
        lblStickerArchivo.Text = Path.GetFileName(path);
    }

    private async void BtnStickerEnviar_Click(object sender, RoutedEventArgs e)
    {
        txtStickerResultado.Text = "Subiendo y enviando...";
        txtStickerResultado.Text = await UploadAndSendAsync(
            txtStickerNumero.Text.Trim(), _stickerArchivoPath,
            (phoneNumber, mediaId) => _whatsAppClient.SendStickerByMediaIdAsync(phoneNumber, mediaId));
    }

    private async void BtnUbicacionEnviar_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(txtUbicacionLatitud.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var latitud) ||
            !double.TryParse(txtUbicacionLongitud.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var longitud))
        {
            txtUbicacionResultado.Text = "Latitud/longitud inválidas.";
            return;
        }

        txtUbicacionResultado.Text = "Enviando...";
        var result = await _whatsAppClient.SendLocationAsync(
            txtUbicacionNumero.Text.Trim(), latitud, longitud,
            EmptyToNull(txtUbicacionNombre.Text), EmptyToNull(txtUbicacionDireccion.Text));
        txtUbicacionResultado.Text = FormatResult(result);
    }

    private async void BtnContactoEnviar_Click(object sender, RoutedEventArgs e)
    {
        var nombreContacto = txtContactoNombre.Text.Trim();
        if (string.IsNullOrWhiteSpace(nombreContacto))
        {
            txtContactoResultado.Text = "El contacto necesita un nombre.";
            return;
        }

        var contact = new WhatsAppContact
        {
            Name = new WhatsAppContactName { FormattedName = nombreContacto },
        };

        var telefonoContacto = txtContactoTelefono.Text.Trim();
        if (!string.IsNullOrWhiteSpace(telefonoContacto))
        {
            contact.Phones.Add(new WhatsAppContactPhone { Phone = telefonoContacto, Type = "CELL" });
        }

        txtContactoResultado.Text = "Enviando...";
        var result = await _whatsAppClient.SendContactsAsync(txtContactoNumero.Text.Trim(), new[] { contact });
        txtContactoResultado.Text = FormatResult(result);
    }

    private async void BtnReaccionEnviar_Click(object sender, RoutedEventArgs e)
    {
        txtReaccionResultado.Text = "Enviando...";
        var result = await _whatsAppClient.SendReactionAsync(
            txtReaccionNumero.Text.Trim(), txtReaccionMessageId.Text.Trim(), txtReaccionEmoji.Text.Trim());
        txtReaccionResultado.Text = FormatResult(result);
    }

    private async void BtnPlantillasListar_Click(object sender, RoutedEventArgs e)
    {
        txtPlantillasResultado.Text = "Consultando...";
        var result = await _whatsAppClient.GetMessageTemplatesAsync();
        txtPlantillasResultado.Text = result.IsSuccess
            ? string.Join(Environment.NewLine, (result.Data ?? Array.Empty<WhatsAppMessageTemplate>())
                .Select(template => $"{template.Name} ({template.Language}) — {template.Status}"))
            : $"Error: {result.Message}";
    }

    private static string? EmptyToNull(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
