using System.Net;

namespace ArjuyWhatsApp.Tests;

/// <summary>
/// <see cref="HttpMessageHandler"/> de prueba que devuelve una respuesta prefijada y captura
/// el último request enviado, para poder inspeccionar URL/headers/body en los asserts sin
/// pegarle nunca a la API real de Meta.
/// </summary>
internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody)
        };
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
