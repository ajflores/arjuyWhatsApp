using Xunit;

namespace ArjuyWhatsApp.Tests;

public class MetaApiErrorTests
{
    [Theory]
    [InlineData(429, null)]
    [InlineData(400, 4)]
    [InlineData(400, 80007)]
    [InlineData(400, 130429)]
    [InlineData(400, 131056)]
    public void IsRateLimited_Http429OCodeConocido_DevuelveTrue(int httpStatusCode, int? code)
    {
        var error = new MetaApiError(httpStatusCode, "{}", "mensaje", code: code);

        Assert.True(error.IsRateLimited);
    }

    [Fact]
    public void IsRateLimited_CodeDesconocidoYNo429_DevuelveFalse()
    {
        var error = new MetaApiError(400, "{}", "Invalid parameter", code: 100);

        Assert.False(error.IsRateLimited);
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    public void IsTransient_SegunStatusHttp(int httpStatusCode, bool expectedTransient)
    {
        var error = new MetaApiError(httpStatusCode, "{}", "mensaje");

        Assert.Equal(expectedTransient, error.IsTransient);
    }

    [Fact]
    public void Constructor_PermiteCamposOpcionalesNulos()
    {
        var error = new MetaApiError(500, "raw body", "mensaje");

        Assert.Null(error.Code);
        Assert.Null(error.ErrorSubcode);
        Assert.Null(error.Type);
        Assert.Null(error.FbTraceId);
        Assert.Equal("raw body", error.RawBody);
    }
}
