using Xunit;

namespace ArjuyWhatsApp.Tests;

public class DefaultPhoneNumberNormalizerTests
{
    private readonly DefaultPhoneNumberNormalizer _normalizer = new();

    [Fact]
    public void Normalize_SoloRecortaEspacios_NoAplicaNingunaOtraRegla()
    {
        Assert.Equal("5491123456781", _normalizer.Normalize("  5491123456781  "));
    }

    [Fact]
    public void Normalize_NoModificaElContenidoDelNumero()
    {
        const string number = "011 15-1234-5678";

        Assert.Equal(number, _normalizer.Normalize(number));
    }
}
