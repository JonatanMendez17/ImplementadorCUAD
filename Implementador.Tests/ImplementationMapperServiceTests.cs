using Implementador.Application.Implementation;
using Implementador.Tests.Helpers;
using Xunit;

namespace Implementador.Tests;

public class ImplementationMapperServiceTests
{
    private readonly ImplementationMapperService _sut = new();
    private readonly FakeLogger _log = new();

    private static Dictionary<string, string> Fila(params (string key, string value)[] pares) =>
        pares.ToDictionary(p => p.key, p => p.value);

    // ── MapPadronSocios ────────────────────────────────────────────────────────

    [Fact]
    public void MapPadronSocios_FilaValida_RetornaRegistroMapeado()
    {
        var data = new List<Dictionary<string, string>>
        {
            Fila(
                ("Entidad", "BDI"),
                ("Nro Socio", "42"),
                ("Fecha Alta Socio", "01/01/2020"),
                ("Documento", "12345678"),
                ("Código Categoría", "A1")
            )
        };

        var result = _sut.MapPadronSocios(data, _log);

        Assert.Single(result);
        var reg = result[0];
        Assert.Equal("BDI", reg.Entidad);
        Assert.Equal(42, reg.NroSocio);
        Assert.Equal(12345678, reg.Documento);
        Assert.Equal("A1", reg.CodigoCategoria);
        Assert.Equal(new DateTime(2020, 1, 1), reg.FechaAltaSocio.Date);
    }

    [Fact]
    public void MapPadronSocios_CUITConGuiones_SeNormalizaCorrectamente()
    {
        var data = new List<Dictionary<string, string>>
        {
            Fila(
                ("Entidad", "BDI"),
                ("Nro Socio", "1"),
                ("Fecha Alta Socio", "01/01/2020"),
                ("Documento", "12345678"),
                ("Código Categoría", "A"),
                ("CUIT", "20-12345678-9")
            )
        };

        var result = _sut.MapPadronSocios(data, _log);

        Assert.Single(result);
        Assert.Equal(20123456789L, result[0].Cuit);
    }

    [Fact]
    public void MapPadronSocios_FilaConCampoRequeridoVacio_SeDescarta()
    {
        var data = new List<Dictionary<string, string>>
        {
            Fila(
                ("Entidad", ""),
                ("Nro Socio", "1"),
                ("Fecha Alta Socio", "01/01/2020"),
                ("Documento", "12345678"),
                ("Código Categoría", "A")
            )
        };

        var result = _sut.MapPadronSocios(data, _log);

        Assert.Empty(result);
    }

    [Fact]
    public void MapPadronSocios_FechaInvalida_SeDescarta()
    {
        var data = new List<Dictionary<string, string>>
        {
            Fila(
                ("Entidad", "BDI"),
                ("Nro Socio", "1"),
                ("Fecha Alta Socio", "no-es-fecha"),
                ("Documento", "12345678"),
                ("Código Categoría", "A")
            )
        };

        var result = _sut.MapPadronSocios(data, _log);

        Assert.Empty(result);
    }

    [Fact]
    public void MapPadronSocios_ListaVacia_RetornaListaVacia()
    {
        var result = _sut.MapPadronSocios([], _log);

        Assert.Empty(result);
    }

    // ── MapConsumos ────────────────────────────────────────────────────────────

    [Fact]
    public void MapConsumos_FilaValida_RetornaRegistroMapeado()
    {
        var data = new List<Dictionary<string, string>>
        {
            Fila(
                ("Entidad", "BDI"),
                ("Nro Socio", "42"),
                ("Código Consumo", "9001"),
                ("Cuotas Pendientes", "12"),
                ("Monto Deuda", "1500.50"),
                ("Concepto Descuento", "5")
            )
        };

        var result = _sut.MapConsumos(data, _log);

        Assert.Single(result);
        var reg = result[0];
        Assert.Equal("BDI", reg.Entidad);
        Assert.Equal(42, reg.NroSocio);
        Assert.Equal(9001L, reg.CodigoConsumo);
        Assert.Equal(12, reg.CuotasPendientes);
        Assert.Equal(1500.50m, reg.MontoDeuda);
    }

    [Fact]
    public void MapConsumos_CampoRequeridoVacio_SeDescarta()
    {
        var data = new List<Dictionary<string, string>>
        {
            Fila(
                ("Entidad", "BDI"),
                ("Nro Socio", ""),
                ("Código Consumo", "100"),
                ("Cuotas Pendientes", "3"),
                ("Monto Deuda", "100"),
                ("Concepto Descuento", "1")
            )
        };

        var result = _sut.MapConsumos(data, _log);

        Assert.Empty(result);
    }

    // ── MapConsumosDetalle ─────────────────────────────────────────────────────

    [Fact]
    public void MapConsumosDetalle_FilaValida_RetornaRegistroMapeado()
    {
        var data = new List<Dictionary<string, string>>
        {
            Fila(
                ("Entidad", "BDI"),
                ("Código Consumo", "9001"),
                ("Nro Cuota", "1"),
                ("Fecha Vencimiento", "01/04/2026"),
                ("Monto", "500.00")
            )
        };

        var result = _sut.MapConsumosDetalle(data, _log);

        Assert.Single(result);
        var reg = result[0];
        Assert.Equal("BDI", reg.Entidad);
        Assert.Equal(9001, reg.CodigoConsumo);
        Assert.Equal(1, reg.NroCuota);
        Assert.Equal(500.00m, reg.Monto);
    }

    [Fact]
    public void MapConsumosDetalle_CampoRequeridoVacio_SeDescarta()
    {
        var data = new List<Dictionary<string, string>>
        {
            Fila(
                ("Entidad", "BDI"),
                ("Código Consumo", ""),
                ("Nro Cuota", "1"),
                ("Fecha Vencimiento", "01/04/2026"),
                ("Monto", "500")
            )
        };

        var result = _sut.MapConsumosDetalle(data, _log);

        Assert.Empty(result);
    }
}
