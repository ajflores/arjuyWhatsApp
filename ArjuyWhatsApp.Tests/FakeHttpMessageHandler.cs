using System.Net;
using System.Net.Http.Headers;

namespace ArjuyWhatsApp.Tests;

/// <summary>
/// <see cref="HttpMessageHandler"/> de prueba que devuelve una secuencia de respuestas prefijadas
/// (una por cada llamado sucesivo — la última se repite si se supera la cantidad configurada) y
/// captura cada request enviado, para poder inspeccionar URL/headers/body/cantidad de intentos en
/// los asserts sin pegarle nunca a la API real de Meta.
/// </summary>
internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(HttpStatusCode StatusCode, string Body, TimeSpan? RetryAfter)> _responses;
    private int _callIndex;

    /// <summary>Todos los requests enviados, en orden. Útil para verificar cuántos intentos hizo el retry.</summary>
    public List<HttpRequestMessage> Requests { get; } = new();

    public HttpRequestMessage? LastRequest => Requests.Count > 0 ? Requests[^1] : null;

    public string? LastRequestBody { get; private set; }

    public int CallCount => Requests.Count;

    /// <summary>Respuesta única, devuelta en todos los llamados (comportamiento previo, sin cambios para los tests existentes).</summary>
    public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody, TimeSpan? retryAfter = null)
        : this(new[] { (statusCode, responseBody, retryAfter) })
    {
    }

    /// <summary>
    /// Secuencia de respuestas: el llamado N devuelve <paramref name="responseSequence"/>[N] (0-indexado);
    /// si hay más llamados que respuestas en la secuencia, se repite la última indefinidamente.
    /// </summary>
    public FakeHttpMessageHandler(IEnumerable<(HttpStatusCode StatusCode, string Body, TimeSpan? RetryAfter)> responseSequence)
    {
        _responses = responseSequence.ToList();
        if (_responses.Count == 0)
        {
            throw new ArgumentException("responseSequence no puede estar vacío.", nameof(responseSequence));
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        var index = Math.Min(_callIndex, _responses.Count - 1);
        var (statusCode, body, retryAfter) = _responses[index];
        _callIndex++;

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        };

        if (retryAfter.HasValue)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter.Value);
        }

        return response;
    }
}

/// <summary>
/// <see cref="IHttpClientFactory"/> de prueba que siempre devuelve un <see cref="HttpClient"/>
/// construido sobre el <see cref="FakeHttpMessageHandler"/> indicado.
/// </summary>
internal class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(_handler, disposeHandler: false);
    }
}
