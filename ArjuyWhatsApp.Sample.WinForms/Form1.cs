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
        BuildFeatureTabs();
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

    // -----------------------------------------------------------------------------------------
    // Pestañas de funcionalidades (imagen, documento, audio, video, sticker, ubicación, contacto,
    // reacción, plantillas), armadas por código en vez de con el diseñador visual — con esa
    // cantidad de campos por pestaña, coordenadas absolutas a mano serían imposibles de mantener.
    // Cada pestaña usa FlowLayoutPanel (TopDown) en vez de TableLayoutPanel: con Dock/anchors la
    // ubicación relativa entre controles agregados en distinto orden puede no ser la esperada sin
    // probarlo visualmente; FlowLayoutPanel simplemente apila en el orden en que se agregan los
    // controles, sin ambigüedad.
    // -----------------------------------------------------------------------------------------

    private enum FieldKind
    {
        Text,
        Number,
        Checkbox,
        File
    }

    private sealed record FieldSpec(string Key, string Label, FieldKind Kind = FieldKind.Text, string Placeholder = "");

    private void BuildFeatureTabs()
    {
        var imagen = BuildTab("Imagen", new[]
        {
            new FieldSpec("Numero", "Número destino:", Placeholder: "5491122334455"),
            new FieldSpec("Archivo", "Archivo:", FieldKind.File),
            new FieldSpec("Caption", "Caption (opcional):"),
        }, "Subir y enviar imagen");

        imagen.SubmitButton.Click += async (_, _) => await HandleMediaSendAsync(
            imagen.Fields, imagen.ResultLabel,
            (phoneNumber, mediaId) => _whatsAppClient.SendImageByMediaIdAsync(phoneNumber, mediaId, GetText(imagen.Fields, "Caption")));

        var documento = BuildTab("Documento", new[]
        {
            new FieldSpec("Numero", "Número destino:", Placeholder: "5491122334455"),
            new FieldSpec("Archivo", "Archivo:", FieldKind.File),
            new FieldSpec("NombreMostrado", "Nombre mostrado (opcional):"),
            new FieldSpec("Caption", "Caption (opcional):"),
        }, "Subir y enviar documento");

        documento.SubmitButton.Click += async (_, _) => await HandleMediaSendAsync(
            documento.Fields, documento.ResultLabel,
            (phoneNumber, mediaId) =>
            {
                var nombreMostrado = GetText(documento.Fields, "NombreMostrado");
                var fileName = string.IsNullOrWhiteSpace(nombreMostrado)
                    ? Path.GetFileName(GetFilePath(documento.Fields, "Archivo") ?? string.Empty)
                    : nombreMostrado;
                return _whatsAppClient.SendDocumentByMediaIdAsync(phoneNumber, mediaId, fileName, GetText(documento.Fields, "Caption"));
            });

        var audio = BuildTab("Audio", new[]
        {
            new FieldSpec("Numero", "Número destino:", Placeholder: "5491122334455"),
            new FieldSpec("Archivo", "Archivo:", FieldKind.File),
            new FieldSpec("Voz", "Mostrar como nota de voz:", FieldKind.Checkbox),
        }, "Subir y enviar audio");

        audio.SubmitButton.Click += async (_, _) => await HandleMediaSendAsync(
            audio.Fields, audio.ResultLabel,
            (phoneNumber, mediaId) => _whatsAppClient.SendAudioByMediaIdAsync(phoneNumber, mediaId, GetChecked(audio.Fields, "Voz")));

        var video = BuildTab("Video", new[]
        {
            new FieldSpec("Numero", "Número destino:", Placeholder: "5491122334455"),
            new FieldSpec("Archivo", "Archivo:", FieldKind.File),
            new FieldSpec("Caption", "Caption (opcional):"),
        }, "Subir y enviar video");

        video.SubmitButton.Click += async (_, _) => await HandleMediaSendAsync(
            video.Fields, video.ResultLabel,
            (phoneNumber, mediaId) => _whatsAppClient.SendVideoByMediaIdAsync(phoneNumber, mediaId, GetText(video.Fields, "Caption")));

        var sticker = BuildTab("Sticker", new[]
        {
            new FieldSpec("Numero", "Número destino:", Placeholder: "5491122334455"),
            new FieldSpec("Archivo", "Archivo (.webp):", FieldKind.File),
        }, "Subir y enviar sticker");

        sticker.SubmitButton.Click += async (_, _) => await HandleMediaSendAsync(
            sticker.Fields, sticker.ResultLabel,
            (phoneNumber, mediaId) => _whatsAppClient.SendStickerByMediaIdAsync(phoneNumber, mediaId));

        var ubicacion = BuildTab("Ubicación", new[]
        {
            new FieldSpec("Numero", "Número destino:", Placeholder: "5491122334455"),
            new FieldSpec("Latitud", "Latitud:", FieldKind.Number, "-54.8019"),
            new FieldSpec("Longitud", "Longitud:", FieldKind.Number, "-68.3030"),
            new FieldSpec("Nombre", "Nombre del lugar (opcional):"),
            new FieldSpec("Direccion", "Dirección (opcional):"),
        }, "Enviar ubicación");

        ubicacion.SubmitButton.Click += async (_, _) =>
        {
            var phoneNumber = GetText(ubicacion.Fields, "Numero");
            var latitudText = GetText(ubicacion.Fields, "Latitud");
            var longitudText = GetText(ubicacion.Fields, "Longitud");

            if (!double.TryParse(latitudText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var latitud) ||
                !double.TryParse(longitudText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var longitud))
            {
                ubicacion.ResultLabel.Text = "Latitud/longitud inválidas.";
                return;
            }

            ubicacion.ResultLabel.Text = "Enviando...";
            var result = await _whatsAppClient.SendLocationAsync(
                phoneNumber, latitud, longitud, GetText(ubicacion.Fields, "Nombre"), GetText(ubicacion.Fields, "Direccion"));
            ubicacion.ResultLabel.Text = FormatResult(result);
        };

        var contacto = BuildTab("Contacto", new[]
        {
            new FieldSpec("Numero", "Número destino:", Placeholder: "5491122334455"),
            new FieldSpec("NombreContacto", "Nombre del contacto:"),
            new FieldSpec("TelefonoContacto", "Teléfono del contacto (opcional):"),
        }, "Enviar contacto");

        contacto.SubmitButton.Click += async (_, _) =>
        {
            var nombreContacto = GetText(contacto.Fields, "NombreContacto");
            if (string.IsNullOrWhiteSpace(nombreContacto))
            {
                contacto.ResultLabel.Text = "El contacto necesita un nombre.";
                return;
            }

            var whatsAppContact = new WhatsAppContact
            {
                Name = new WhatsAppContactName { FormattedName = nombreContacto },
            };

            var telefonoContacto = GetText(contacto.Fields, "TelefonoContacto");
            if (!string.IsNullOrWhiteSpace(telefonoContacto))
            {
                whatsAppContact.Phones.Add(new WhatsAppContactPhone { Phone = telefonoContacto, Type = "CELL" });
            }

            contacto.ResultLabel.Text = "Enviando...";
            var result = await _whatsAppClient.SendContactsAsync(GetText(contacto.Fields, "Numero"), new[] { whatsAppContact });
            contacto.ResultLabel.Text = FormatResult(result);
        };

        var reaccion = BuildTab("Reacción", new[]
        {
            new FieldSpec("Numero", "Número destino:", Placeholder: "5491122334455"),
            new FieldSpec("MessageId", "Message id (wamid.):"),
            new FieldSpec("Emoji", "Emoji (vacío = remover):", Placeholder: "👍"),
        }, "Reaccionar");

        reaccion.SubmitButton.Click += async (_, _) =>
        {
            reaccion.ResultLabel.Text = "Enviando...";
            var result = await _whatsAppClient.SendReactionAsync(
                GetText(reaccion.Fields, "Numero"), GetText(reaccion.Fields, "MessageId"), GetText(reaccion.Fields, "Emoji"));
            reaccion.ResultLabel.Text = FormatResult(result);
        };

        var plantillas = BuildTab("Plantillas", Array.Empty<FieldSpec>(), "Listar plantillas aprobadas");

        plantillas.SubmitButton.Click += async (_, _) =>
        {
            plantillas.ResultLabel.Text = "Consultando...";
            var result = await _whatsAppClient.GetMessageTemplatesAsync();
            plantillas.ResultLabel.Text = result.IsSuccess
                ? string.Join(Environment.NewLine, (result.Data ?? Array.Empty<WhatsAppMessageTemplate>())
                    .Select(template => $"{template.Name} ({template.Language}) — {template.Status}"))
                : $"Error: {result.Message}";
        };
    }

    private static string FormatResult(MResult<string> result)
    {
        return result.IsSuccess
            ? $"Enviado. Message id: {result.Data}"
            : $"Error: {result.Message}";
    }

    /// <summary>
    /// Sube <c>Archivo</c> (tomado de <paramref name="fields"/>) a Meta y, si la subida fue exitosa,
    /// ejecuta <paramref name="send"/> con el número destino y el <c>media_id</c> resultante — mismo
    /// mecanismo de un solo paso que <c>WhatsAppMediaController.SendMedia</c> en Sample.Api.
    /// </summary>
    private async Task HandleMediaSendAsync(
        Dictionary<string, Control> fields,
        Label resultLabel,
        Func<string, string, Task<MResult<string>>> send)
    {
        var phoneNumber = GetText(fields, "Numero");
        var filePath = GetFilePath(fields, "Archivo");

        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(filePath))
        {
            resultLabel.Text = "Completá el número y elegí un archivo.";
            return;
        }

        resultLabel.Text = "Subiendo y enviando...";

        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var fileName = Path.GetFileName(filePath);

        var uploadResult = await _whatsAppClient.UploadMediaAsync(fileBytes, fileName, GuessMimeType(fileName));
        if (!uploadResult.IsSuccess || uploadResult.Data is null)
        {
            resultLabel.Text = $"Error al subir: {uploadResult.Message}";
            return;
        }

        var sendResult = await send(phoneNumber, uploadResult.Data);
        resultLabel.Text = FormatResult(sendResult);
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

    private static string GetText(Dictionary<string, Control> fields, string key)
    {
        return fields.TryGetValue(key, out var control) ? control.Text.Trim() : string.Empty;
    }

    private static bool GetChecked(Dictionary<string, Control> fields, string key)
    {
        return fields.TryGetValue(key, out var control) && control is CheckBox checkBox && checkBox.Checked;
    }

    private static string? GetFilePath(Dictionary<string, Control> fields, string key)
    {
        return fields.TryGetValue(key, out var control) ? control.Tag as string : null;
    }

    /// <summary>Botón "Elegir archivo..." que guarda la ruta completa elegida en su <see cref="Control.Tag"/> y muestra el nombre de archivo como texto.</summary>
    private static Button BuildFilePicker()
    {
        var button = new Button
        {
            Text = "Elegir archivo...",
            AutoSize = true,
        };

        button.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                button.Tag = dialog.FileName;
                button.Text = Path.GetFileName(dialog.FileName);
            }
        };

        return button;
    }

    private (Dictionary<string, Control> Fields, Button SubmitButton, Label ResultLabel) BuildTab(
        string title, IReadOnlyList<FieldSpec> fieldSpecs, string buttonText)
    {
        var page = new TabPage(title);
        tabFuncionalidades.TabPages.Add(page);

        var outer = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(8),
        };

        var fields = new Dictionary<string, Control>();

        foreach (var spec in fieldSpecs)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6),
            };

            var label = new Label
            {
                Text = spec.Label,
                AutoSize = true,
                Width = 170,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 6, 0),
            };
            row.Controls.Add(label);

            Control input = spec.Kind switch
            {
                FieldKind.Checkbox => new CheckBox { AutoSize = true },
                FieldKind.File => BuildFilePicker(),
                _ => new TextBox { Width = 220, PlaceholderText = spec.Placeholder },
            };
            row.Controls.Add(input);
            fields[spec.Key] = input;

            outer.Controls.Add(row);
        }

        var submitButton = new Button { Text = buttonText, AutoSize = true, Margin = new Padding(0, 4, 0, 10) };
        outer.Controls.Add(submitButton);

        var resultLabel = new Label { AutoSize = false, Height = 60, Width = 380, Margin = new Padding(0) };
        outer.Controls.Add(resultLabel);

        page.Controls.Add(outer);

        return (fields, submitButton, resultLabel);
    }
}
