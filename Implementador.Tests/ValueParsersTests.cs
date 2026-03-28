using Implementador.Application.Validation.Common;
using Xunit;

namespace Implementador.Tests;

public class ValueParsersTests
{
    // ── TryParseDecimalFlexible ────────────────────────────────────────────────

    [Theory]
    [InlineData("1234.56", 1234.56)]
    [InlineData("0.01", 0.01)]
    [InlineData("100", 100)]
    [InlineData("  99.9  ", 99.9)]
    public void TryParseDecimalFlexible_ValoresValidos_RetornaTrue(string input, double expected)
    {
        var result = ValueParsers.TryParseDecimalFlexible(input, out var value);

        Assert.True(result);
        Assert.Equal((decimal)expected, value, 4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData((string?)null)]
    public void TryParseDecimalFlexible_ValoresInvalidos_RetornaFalse(string? input)
    {
        var result = ValueParsers.TryParseDecimalFlexible(input, out _);

        Assert.False(result);
    }

    // ── TryParseDateFlexible ───────────────────────────────────────────────────

    [Theory]
    [InlineData("15/03/2026")]
    [InlineData("2026-03-15")]
    [InlineData("03/15/2026")]
    [InlineData("15-03-2026")]
    public void TryParseDateFlexible_FechasValidas_RetornaTrue(string input)
    {
        var result = ValueParsers.TryParseDateFlexible(input, out var date);

        Assert.True(result);
        Assert.Equal(new DateTime(2026, 3, 15), date.Date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-fecha")]
    [InlineData("32/01/2026")]
    [InlineData((string?)null)]
    public void TryParseDateFlexible_FechasInvalidas_RetornaFalse(string? input)
    {
        var result = ValueParsers.TryParseDateFlexible(input, out _);

        Assert.False(result);
    }

    // ── EqualsTrimmed ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ABC", "ABC")]
    [InlineData("  abc  ", "ABC")]
    [InlineData("abc", "ABC")]
    [InlineData("", "")]
    public void EqualsTrimmed_CasosDondeDebeSer_True(string left, string right)
    {
        Assert.True(ValueParsers.EqualsTrimmed(left, right));
    }

    [Theory]
    [InlineData("ABC", "DEF")]
    [InlineData("abc", "abcd")]
    [InlineData("", "x")]
    public void EqualsTrimmed_CasosDondeDebeSer_False(string left, string right)
    {
        Assert.False(ValueParsers.EqualsTrimmed(left, right));
    }

    [Fact]
    public void EqualsTrimmed_AmbosSonNull_RetornaTrue()
    {
        Assert.True(ValueParsers.EqualsTrimmed(null, null));
    }

    [Fact]
    public void EqualsTrimmed_UnNullOtroNoNull_RetornaFalse()
    {
        Assert.False(ValueParsers.EqualsTrimmed(null, "valor"));
        Assert.False(ValueParsers.EqualsTrimmed("valor", null));
    }

    // ── EqualsDigitsOnly ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("20-12345678-9", "20123456789")]
    [InlineData("20.12345678.9", "20123456789")]
    [InlineData("20123456789", "20123456789")]
    public void EqualsDigitsOnly_MismosDigitos_RetornaTrue(string left, string right)
    {
        Assert.True(ValueParsers.EqualsDigitsOnly(left, right));
    }

    [Theory]
    [InlineData("20-12345678-9", "27-12345678-4")]
    [InlineData("123", "456")]
    public void EqualsDigitsOnly_DigitosDistintos_RetornaFalse(string left, string right)
    {
        Assert.False(ValueParsers.EqualsDigitsOnly(left, right));
    }

    [Fact]
    public void EqualsDigitsOnly_AmbosSonNull_RetornaTrue()
    {
        Assert.True(ValueParsers.EqualsDigitsOnly(null, null));
    }

    [Fact]
    public void EqualsDigitsOnly_UnNullOtroNoNull_RetornaFalse()
    {
        Assert.False(ValueParsers.EqualsDigitsOnly(null, "123"));
        Assert.False(ValueParsers.EqualsDigitsOnly("123", null));
    }
}
