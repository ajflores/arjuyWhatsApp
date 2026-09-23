using System.Windows;
using ArjuyWhatsApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// -----------------------------------------------------------------------
// ArjuyWhatsApp.Sample.Wpf — sample de escritorio (WPF) que envía Y recibe
// (webhook) mensajes de WhatsApp Cloud API, igual que ArjuyWhatsApp.Sample.Api,
// pero desde una app de escritorio.
//
// EL DESAFÍO: WPF no tiene forma nativa de exponer un endpoint HTTP. Meta
// necesita poder pegarle a una URL pública (GET para el verify challenge, POST
// para cada mensaje entrante) — eso es un servidor HTTP, y una app de
// escritorio no es un servidor HTTP por sí sola.
//
// LA SOLUCIÓN: levantar un mini-host ASP.NET Core (Kestrel) EMBEBIDO dentro
// del mismo proceso de WPF, corriendo en background (Task.Run), cuyo único
// trabajo es atender /api/webhooks/whatsapp vía MapArjuyWhatsAppWebhook(). No
// tiene UI propia, no sirve páginas, no compite con la MainWindow — conviven
// en el mismo proceso: threads del pool atendiendo HTTP (Kestrel), y el
// Dispatcher thread de WPF atendiendo la ventana.
//
// PUERTO FIJO: este host embebido escucha en el puerto 7200 (a diferencia de
// ArjuyWhatsApp.Sample.Api, que usa 5100/7100 vía launchSettings.json). Para
// probar la recepción de mensajes reales con Meta en este sample, ngrok tiene
// que apuntar a ESE puerto:
//
//     ngrok http 7200
//
// y la Callback URL configurada en el panel de Meta for Developers debe ser
// "https://<tu-subdominio-ngrok>.ngrok-free.app/api/webhooks/whatsapp".
// -----------------------------------------------------------------------

namespace ArjuyWhatsApp.Sample.Wpf;

public partial class App : Application
{
    private WebApplication? _webHost;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = WebApplication.CreateBuilder();

        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        // Puerto fijo del host embebido — ver comentario arriba. Kestrel explícito en vez de
        // dejar que tome el default, para que quede documentado en un solo lugar cuál es el
        // puerto al que hay que apuntar ngrok.
        builder.WebHost.UseUrls("http://localhost:7200");

        builder.Services.AddArjuyWhatsApp(builder.Configuration);

        _webHost = builder.Build();

        // Registra GET+POST en /api/webhooks/whatsapp por default (verify challenge + recepción
        // de mensajes). Ver ArjuyWhatsApp.Sample.Api/Program.cs para el mismo mecanismo con
        // Sdk.Web + Controllers; acá no hace falta ninguno de los dos, MapArjuyWhatsAppWebhook
        // alcanza.
        _webHost.MapArjuyWhatsAppWebhook();

        var whatsAppClient = _webHost.Services.GetRequiredService<IArjuyWhatsAppClient>();

        // El host Kestrel se arranca en background: WPF necesita su propio Dispatcher thread
        // libre para el message loop de la UI, así que el servidor HTTP embebido vive en threads
        // del pool aparte, no en el Dispatcher thread.
        _ = Task.Run(() => _webHost.RunAsync());

        var mainWindow = new MainWindow(whatsAppClient);

        // ⚠️ IMPORTANTE: MessageReceived se dispara desde el thread de background de Kestrel que
        // atendió el POST del webhook (uno del thread pool, NO el Dispatcher thread de WPF). Por
        // eso la suscripción va a un método de la ventana que hace el marshaling correcto con
        // Dispatcher.Invoke antes de tocar la ObservableCollection bindeada — ver
        // MainWindow.OnMessageReceived.
        whatsAppClient.MessageReceived += mainWindow.OnMessageReceived;

        // Mismo mecanismo de threading que MessageReceived (ver comentario arriba) — este evento
        // también se dispara desde el thread de background de Kestrel. Ver
        // MainWindow.OnMessageStatusUpdated para el indicador visual ✓✓ de estado de entrega.
        whatsAppClient.MessageStatusUpdated += mainWindow.OnMessageStatusUpdated;

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_webHost is not null)
        {
            await _webHost.StopAsync();
        }

        base.OnExit(e);
    }
}
