using Implementador.Application.Validation.Common;
using Xunit;

namespace Implementador.Tests;

public class RowValueReaderTests
{
    private static Dictionary<string, string> Fila(params (string key, string value)[] pares) =>
        pares.ToDictionary(p => p.key, p => p.value);

    // ── TryGetFirstValue ───────────────────────────────────────────────────────

    [Fact]
    public void TryGetFirstValue_PrimeraClaveExiste_RetornaTrueYValor()
    {
        var row = Fila(("Entidad", "BDI"), ("Nro Socio", "100"));

        var found = RowValueReader.TryGetFirstValue(row, out var value, "Entidad", "Institucion");

        Assert.True(found);
        Assert.Equal("BDI", value);
    }

    [Fact]
    public void TryGetFirstValue_PrimeraClaveNoExistePeroAliasExiste_RetornaTrueYValorDelAlias()
    {
        var row = Fila(("Institucion", "BDI"));

        var found = RowValueReader.TryGetFirstValue(row, out var value, "Entidad", "Institucion");

        Assert.True(found);
        Assert.Equal("BDI", value);
    }

    [Fact]
    public void TryGetFirstValue_NingunaClaveExiste_RetornaFalseYVacío()
    {
        var row = Fila(("OtraClave", "valor"));

        var found = RowValueReader.TryGetFirstValue(row, out var value, "Entidad", "Institucion");

        Assert.False(found);
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void TryGetFirstValue_FilaVacia_RetornaFalse()
    {
        var row = new Dictionary<string, string>();

        var found = RowValueReader.TryGetFirstValue(row, out var value, "Entidad");

        Assert.False(found);
        Assert.Equal(string.Empty, value);
    }

    // ── GetFirstValue ──────────────────────────────────────────────────────────

    [Fact]
    public void GetFirstValue_ClaveExiste_RetornaValor()
    {
        var row = Fila(("Entidad", "ACTIVOS"));

        var value = RowValueReader.GetFirstValue(row, "Entidad");

        Assert.Equal("ACTIVOS", value);
    }

    [Fact]
    public void GetFirstValue_ClaveNoExiste_RetornaVacío()
    {
        var row = Fila(("OtraClave", "valor"));

        var value = RowValueReader.GetFirstValue(row, "Entidad");

        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void GetFirstValue_RetornaPrimerCoincidencia_CuandoHayMultiplesClaves()
    {
        var row = Fila(("Entidad", "BDI"), ("Institucion", "OTRO"));

        var value = RowValueReader.GetFirstValue(row, "Entidad", "Institucion");

        Assert.Equal("BDI", value);
    }
}
