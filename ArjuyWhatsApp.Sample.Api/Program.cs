using ArjuyWhatsApp;

// -----------------------------------------------------------------------
// ArjuyWhatsApp.Sample.Api — sample con servidor HTTP real, para poder probar
// de punta a punta el envío Y la recepción (webhook) de WhatsApp Cloud API.
//
// A diferencia de ArjuyWhatsApp.Sample (consola), este proyecto expone un
// endpoint HTTP real que Meta puede alcanzar (vía ngrok en desarrollo local),
// necesario porque una consola sola no puede recibir un webhook entrante.
//
// Acá conviven, a propósito, LOS DOS mecanismos de recepción que ofrece la
// librería, uno al lado del otro:
//   - Evento MessageReceived (suscripción ad-hoc más abajo, después de Build()).
//   - IWhatsAppMessageHandler por DI (LoggingMessageHandler, registrado abajo).
// Ver MANUAL.md sección 3 para el detalle de ambos mecanismos.
// -----------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI: forma más cómoda de disparar los endpoints de envío (WhatsAppSendController)
// desde el navegador mientras se debuggea en Visual Studio, sin necesitar Postman/curl aparte.
// Ver MANUAL.md, sección "Debugging con Visual Studio".
builder.Services.AddSwaggerGen();

// Registro del cliente de ArjuyWhatsApp leyendo la sección "ArjuyWhatsApp" de appsettings.json.
builder.Services.AddArjuyWhatsApp(builder.Configuration);

// Mecanismo 1: handler resuelto por DI. Se invoca para CADA mensaje entrante que llega vía
// ProcessWebhookAsync, junto con el evento MessageReceived (mecanismo 2, ver más abajo).
builder.Services.AddScoped<IWhatsAppMessageHandler, LoggingMessageHandler>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

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
    // 🔴 PONÉ UN BREAKPOINT ACÁ para inspeccionar el mensaje recibido por EVENTO
    var message = args.Message;
    eventLogger.LogInformation(
        "[EVENTO MessageReceived] Mensaje de {From} ({Type}, id {MessageId}): {Text}",
        message.From, message.Type, message.MessageId, message.Text);
};

app.Run();

/// <summary>
/// Handler de ejemplo registrado por DI (mecanismo 2 de recepción). Solo loguea el mensaje
/// recibido — en un caso real, acá iría la lógica de negocio (guardar en base, resolver un
/// DbContext scoped, etc., ver MANUAL.md sección 3.4.2).
/// </summary>
public class LoggingMessageHandler : IWhatsAppMessageHandler
{
    private readonly ILogger<LoggingMessageHandler> _logger;

    public LoggingMessageHandler(ILogger<LoggingMessageHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(WhatsAppMessageReceived message, CancellationToken cancellationToken = default)
    {
        // 🔴 PONÉ UN BREAKPOINT ACÁ para inspeccionar el mensaje recibido por HANDLER DI
        _logger.LogInformation(
            "[DI IWhatsAppMessageHandler] Mensaje de {From} ({Type}, id {MessageId}): {Text}",
            message.From, message.Type, message.MessageId, message.Text);

        return Task.CompletedTask;
    }
}
