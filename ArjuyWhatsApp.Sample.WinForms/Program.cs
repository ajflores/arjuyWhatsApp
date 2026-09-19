using ArjuyWhatsApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// -----------------------------------------------------------------------
// ArjuyWhatsApp.Sample.WinForms — sample de escritorio (Windows Forms) que
// envía Y recibe (webhook) mensajes de WhatsApp Cloud API, igual que
// ArjuyWhatsApp.Sample.Api y ArjuyWhatsApp.Sample.Wpf, pero desde WinForms.
//
// EL DESAFÍO: igual que en el sample WPF — WinForms no tiene forma nativa de
// exponer un endpoint HTTP. Meta necesita poder pegarle a una URL pública
// (GET para el verify challenge, POST para cada mensaje entrante) — eso es
// un servidor HTTP, y una app de escritorio no es un servidor HTTP por sí sola.
//
// LA SOLUCIÓN: levantar un mini-host ASP.NET Core (Kestrel) EMBEBIDO dentro
// del mismo proceso de WinForms, corriendo en background (Task.Run), cuyo
// único trabajo es atender /api/webhooks/whatsapp vía MapArjuyWhatsAppWebhook().
// No tiene UI propia, no sirve páginas, no compite con el Form principal —
// conviven en el mismo proceso: threads del pool atendiendo HTTP (Kestrel),
// y el UI thread de WinForms atendiendo el message loop de Application.Run.
//
// PUERTO FIJO: este host embebido escucha en el puerto 7300 — distinto del
// 7200 que usa ArjuyWhatsApp.Sample.Wpf y del 7100 de ArjuyWhatsApp.Sample.Api,
// justamente para poder correr los tres samples al mismo tiempo sin pisarse.
// Para probar la recepción de mensajes reales con Meta en este sample, ngrok
// tiene que apuntar a ESE puerto:
//
//     ngrok http 7300
//
// y la Callback URL configurada en el panel de Meta for Developers debe ser
// "https://<tu-subdominio-ngrok>.ngrok-free.app/api/webhooks/whatsapp".
// -----------------------------------------------------------------------

namespace ArjuyWhatsApp.Sample.WinForms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var builder = WebApplication.CreateBuilder();

        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        // Puerto fijo del host embebido — ver comentario arriba. Kestrel explícito en vez de
        // dejar que tome el default, para que quede documentado en un solo lugar cuál es el
        // puerto al que hay que apuntar ngrok.
        builder.WebHost.UseUrls("http://localhost:7300");

        builder.Services.AddArjuyWhatsApp(builder.Configuration);

        var webHost = builder.Build();

        // Registra GET+POST en /api/webhooks/whatsapp por default (verify challenge + recepción
        // de mensajes). Mismo mecanismo que ArjuyWhatsApp.Sample.Wpf/App.xaml.cs.
        webHost.MapArjuyWhatsAppWebhook();

        var whatsAppClient = webHost.Services.GetRequiredService<IArjuyWhatsAppClient>();

        // El host Kestrel se arranca en background: WinForms necesita su propio UI thread libre
        // para el message loop de Application.Run, así que el servidor HTTP embebido vive en
        // threads del pool aparte, no en el UI thread.
        _ = Task.Run(() => webHost.RunAsync());

        var form = new Form1(whatsAppClient);

        // ⚠️ IMPORTANTE: MessageReceived se dispara desde el thread de background de Kestrel que
        // atendió el POST del webhook (uno del thread pool, NO el UI thread de WinForms). Por eso
        // la suscripción va a un método del form que hace el marshaling correcto con
        // this.Invoke/this.BeginInvoke antes de tocar el ListBox — ver Form1.OnMessageReceived.
        whatsAppClient.MessageReceived += form.OnMessageReceived;

        try
        {
            Application.Run(form);
        }
        finally
        {
            webHost.StopAsync().GetAwaiter().GetResult();
        }
    }
}
