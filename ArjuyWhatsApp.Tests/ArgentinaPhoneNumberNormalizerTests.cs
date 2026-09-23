using Xunit;

namespace ArjuyWhatsApp.Tests;

public class ArgentinaPhoneNumberNormalizerTests
{
    private readonly ArgentinaPhoneNumberNormalizer _normalizer = new();

    [Theory]
    [InlineData("5491123456781", "5491123456781")]
    [InlineData("91123456781", "5491123456781")]
    [InlineData("1123456781", "5491123456781")]
    [InlineData("01123456781", "5491123456781")]
    public void Normalize_CasosSinFormatoLocal15_DevuelveFormatoCorrecto(string input, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input));
    }

    [Theory]
    [InlineData("011 15-1234-5678")]
    [InlineData("011-15-1234-5678")]
    [InlineData("(011) 15 1234 5678")]
    public void Normalize_FormatoLocalCabaConTruncoY15_InsertaNueveYSacaQuinceYCero(string input)
    {
        var result = _normalizer.Normalize(input);

        Assert.Equal("5491112345678", result);
    }

    [Fact]
    public void Normalize_NumeroYaCorrecto_EsIdempotente()
    {
        var once = _normalizer.Normalize("011 15-1234-5678");
        var twice = _normalizer.Normalize(once);

        Assert.Equal(once, twice);
        Assert.Equal("5491112345678", twice);
    }

    [Fact]
    public void Normalize_AplicadoDosVecesSobreNumeroYaEnFormatoMeta_NoCambia()
    {
        const string alreadyCorrect = "5491123456789";

        var result = _normalizer.Normalize(alreadyCorrect);

        Assert.Equal(alreadyCorrect, result);
    }

    [Fact]
    public void Normalize_ConEspaciosYGuiones_LosIgnoraYNormalizaIgual()
    {
        var withPunctuation = _normalizer.Normalize("+54 9 11 2345-6781");
        var digitsOnly = _normalizer.Normalize("5491123456781");

        Assert.Equal(digitsOnly, withPunctuation);
    }

    [Fact]
    public void Normalize_CadenaVacia_DevuelveVacia()
    {
        Assert.Equal(string.Empty, _normalizer.Normalize(string.Empty));
    }

    [Fact]
    public void Normalize_SoloEspacios_DevuelveTalCual()
    {
        Assert.Equal("   ", _normalizer.Normalize("   "));
    }
}
