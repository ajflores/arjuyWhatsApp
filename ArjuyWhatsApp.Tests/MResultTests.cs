using Xunit;

namespace ArjuyWhatsApp.Tests;

public class MResultTests
{
    [Fact]
    public void Success_NoGenerico_DevuelveIsSuccessTrue()
    {
        var result = MResult.Success("todo ok");

        Assert.True(result.IsSuccess);
        Assert.Equal("todo ok", result.Message);
    }

    [Fact]
    public void Fail_NoGenerico_DevuelveIsSuccessFalse()
    {
        var result = MResult.Fail("algo salió mal");

        Assert.False(result.IsSuccess);
        Assert.Equal("algo salió mal", result.Message);
    }

    [Fact]
    public void Success_Generico_DevuelveDataYIsSuccessTrue()
    {
        var result = MResult<string>.Success("wamid.123");

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.123", result.Data);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Fail_Generico_DevuelveDataPorDefectoYIsSuccessFalse()
    {
        var result = MResult<string>.Fail("error de Meta");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal("error de Meta", result.Message);
    }
}
