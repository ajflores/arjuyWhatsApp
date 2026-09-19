using ArjuyWhatsApp;
using ArjuyWhatsApp.Sample.Web;

// -----------------------------------------------------------------------
// ArjuyWhatsApp.Sample.Web — sample tipo "chat" con Razor Pages: una única
// página (Pages/Index) para mandar mensajes de texto y ver los recibidos.
//
// Se eligió Razor Pages (y no Blazor Server/WASM ni MVC completo) a propósito:
// para esta sample alcanza con un form clásico que hace POST y recarga la
// página (patrón PRG - Post/Redirect/Get) — no hace falta el canal
// persistente de SignalR de Blazor Server ni el runtime WASM en el cliente,
// y Razor Pages evita la ceremonia de un Controller + vista MVC separados
// para un caso de un solo endpoint. Ver notas de arquitectura guardadas en
// Engram (topic "arjuywhatsapp/sample-web") para el detalle de la decisión.
//
// Igual que en ArjuyWhatsApp.Sample.Api, conviven a propósito los dos
// mecanismos de recepción de la librería: el handler por DI
// (StoringMessageHandler, que persiste en IMessageStore) y el evento
// MessageReceived (suscripción ad-hoc más abajo, solo para loguear).
// -----------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Registro del cliente de ArjuyWhatsApp leyendo la sección "ArjuyWhatsApp" de appsettings.json.
builder.Services.AddArjuyWhatsApp(builder.Configuration);

// Almacén en memoria de mensajes recibidos, consumido por Pages/Index para listarlos.
// Singleton: tiene que sobrevivir entre requests (cada request HTTP resolvería un handler propio).
builder.Services.AddSingleton<IMessageStore, InMemoryMessageStore>();

// Mecanismo 1: handler resuelto por DI. Se invoca para CADA mensaje entrante que llega vía
// ProcessWebhookAsync, junto con el evento MessageReceived (mecanismo 2, ver más abajo).
builder.Services.AddScoped<IWhatsAppMessageHandler, StoringMessageHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();

// Registra GET+POST en /api/webhooks/whatsapp por default — sin Controller propio.
// Ver MANUAL.md sección 3.3/3.4 si necesitás más control (lógica adicional en el endpoint).
app.MapArjuyWhatsAppWebhook();

// Mecanismo 2: evento MessageReceived. Suscripción liviana/ad-hoc, después de construir el
// IServiceProvider — el cliente es Singleton a propósito (ver ServiceCollectionExtensions),
// así que esta suscripción vive durante toda la vida de la app, no por request.
var whatsAppClient = app.Services.GetRequiredService<IArjuyWhatsAppClient>();
var eventLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MessageReceivedEvent");

whatsAppClient.MessageReceived += (sender, args) =>
{
    var message = args.Message;
    eventLogger.LogInformation(
        "[EVENTO MessageReceived] Mensaje de {From} ({Type}, id {MessageId}): {Text}",
        message.From, message.Type, message.MessageId, message.Text);
};

app.Run();
