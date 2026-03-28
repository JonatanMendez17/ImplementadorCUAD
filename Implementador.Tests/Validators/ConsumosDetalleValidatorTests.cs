using Implementador.Application.Validation;
using Implementador.Application.Validation.Core;
using Implementador.Models;
using Implementador.Tests.Helpers;
using Xunit;

namespace Implementador.Tests.Validators;

public class ConsumosDetalleValidatorTests
{
    private readonly ConsumosDetalleValidator _sut = new();
    private readonly FakeLogger _log = new();

    private static Dictionary<string, string> Fila(params (string key, string value)[] pares) =>
        pares.ToDictionary(p => p.key, p => p.value);

    private static ValidationReferenceData SnapshotConEntidad(string entidad) =>
        new()
        {
            EntidadesRef = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entidad }
        };

    private static Dictionary<string, string> FilaConsumo(string codigo, int cuotas, decimal monto) =>
        Fila(
            ("Entidad", "BDI"),
            ("Nro Socio", "10"),
            ("Código Consumo", codigo),
            ("Cuotas Pendientes", cuotas.ToString()),
            ("Monto Deuda", monto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
            ("Concepto Descuento", "")
        );

    private static Dictionary<string, string> FilaDetalle(string codigo, int nroCuota, string fecha, decimal monto) =>
        Fila(
            ("Entidad", "BDI"),
            ("Código Consumo", codigo),
            ("Nro Cuota", nroCuota.ToString()),
            ("Fecha Vencimiento", fecha),
            ("Monto", monto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
        );

    // ── Tests de filas válidas ─────────────────────────────────────────────────

    [Fact]
    public void Apply_DetalleValidoConAgregadosCorrecto_SeAcepta()
    {
        var consumos = new List<Dictionary<string, string>> { FilaConsumo("9001", 2, 1000m) };
        var detalle = new List<Dictionary<string, string>>
        {
            FilaDetalle("9001", 1, "01/06/2026", 500m),
            FilaDetalle("9001", 2, "01/07/2026", 500m)
        };
        var result = new ImplementationValidationResult
        {
            DatosConsumosValidados = consumos,
            DatosConsumosDetalleValidados = detalle,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Equal(2, result.DatosConsumosDetalleValidados.Count);
    }

    [Fact]
    public void Apply_CodigoConsumoNoExisteEnPadre_SeRechaza()
    {
        var consumos = new List<Dictionary<string, string>> { FilaConsumo("9001", 1, 500m) };
        var detalle = new List<Dictionary<string, string>>
        {
            FilaDetalle("9999", 1, "01/06/2026", 500m)
        };
        var result = new ImplementationValidationResult
        {
            DatosConsumosValidados = consumos,
            DatosConsumosDetalleValidados = detalle,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Empty(result.DatosConsumosDetalleValidados);
    }

    [Fact]
    public void Apply_CantidadCuotasNoCoincide_SeRechazaTodo()
    {
        var consumos = new List<Dictionary<string, string>> { FilaConsumo("9001", 3, 900m) };
        var detalle = new List<Dictionary<string, string>>
        {
            FilaDetalle("9001", 1, "01/06/2026", 300m),
            FilaDetalle("9001", 2, "01/07/2026", 300m)
            // Falta cuota 3 — solo 2 de 3
        };
        var result = new ImplementationValidationResult
        {
            DatosConsumosValidados = consumos,
            DatosConsumosDetalleValidados = detalle,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Empty(result.DatosConsumosDetalleValidados);
    }

    [Fact]
    public void Apply_SumaDetalleNoCoincideConMontoDeuda_SeRechazaTodo()
    {
        var consumos = new List<Dictionary<string, string>> { FilaConsumo("9001", 2, 1000m) };
        var detalle = new List<Dictionary<string, string>>
        {
            FilaDetalle("9001", 1, "01/06/2026", 400m),
            FilaDetalle("9001", 2, "01/07/2026", 400m)   // suma 800 ≠ 1000
        };
        var result = new ImplementationValidationResult
        {
            DatosConsumosValidados = consumos,
            DatosConsumosDetalleValidados = detalle,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Empty(result.DatosConsumosDetalleValidados);
    }

    [Fact]
    public void Apply_NrosCuotaNoConsecutivos_SeRechazaTodo()
    {
        var consumos = new List<Dictionary<string, string>> { FilaConsumo("9001", 2, 1000m) };
        var detalle = new List<Dictionary<string, string>>
        {
            FilaDetalle("9001", 1, "01/06/2026", 500m),
            FilaDetalle("9001", 3, "01/07/2026", 500m)   // saltea el 2
        };
        var result = new ImplementationValidationResult
        {
            DatosConsumosValidados = consumos,
            DatosConsumosDetalleValidados = detalle,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Empty(result.DatosConsumosDetalleValidados);
    }

    [Fact]
    public void Apply_FechaVencimientoPasada_SeRechaza()
    {
        var consumos = new List<Dictionary<string, string>> { FilaConsumo("9001", 1, 500m) };
        var detalle = new List<Dictionary<string, string>>
        {
            FilaDetalle("9001", 1, "01/01/2020", 500m)   // fecha pasada
        };
        var result = new ImplementationValidationResult
        {
            DatosConsumosValidados = consumos,
            DatosConsumosDetalleValidados = detalle,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Empty(result.DatosConsumosDetalleValidados);
    }

    [Fact]
    public void Apply_EntidadNoExisteEnReferencia_SeRechaza()
    {
        var consumos = new List<Dictionary<string, string>> { FilaConsumo("9001", 1, 500m) };
        var detalle = new List<Dictionary<string, string>>
        {
            FilaDetalle("9001", 1, "01/06/2026", 500m)
        };
        detalle[0]["Entidad"] = "DESCONOCIDA";

        var result = new ImplementationValidationResult
        {
            DatosConsumosValidados = consumos,
            DatosConsumosDetalleValidados = detalle,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Empty(result.DatosConsumosDetalleValidados);
    }

    [Fact]
    public void Apply_SinDetalle_NoHaceNada()
    {
        var result = new ImplementationValidationResult
        {
            DatosConsumosValidados = [],
            DatosConsumosDetalleValidados = [],
            HasLoadedData = false
        };

        _sut.Apply(result, _log);

        Assert.Empty(result.DatosConsumosDetalleValidados);
        Assert.False(_log.HasErrors);
    }
}
