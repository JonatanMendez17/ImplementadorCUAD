using Implementador.Application.Validation;
using Implementador.Application.Validation.Core;
using Implementador.Models;
using Implementador.Tests.Helpers;
using Xunit;

namespace Implementador.Tests.Validators;

public class ServiciosValidatorTests
{
    private readonly ServiciosValidator _sut = new();
    private readonly FakeLogger _log = new();

    private static Dictionary<string, string> Fila(params (string key, string value)[] pares) =>
        pares.ToDictionary(p => p.key, p => p.value);

    private static ValidationReferenceData SnapshotConEntidad(string entidad) =>
        new()
        {
            EntidadesRef = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entidad }
        };

    private static Dictionary<string, string> FilaPadron(string nroSocio, string cuit = "", string beneficio = "") =>
        Fila(
            ("Entidad", "BDI"),
            ("Nro Socio", nroSocio),
            ("CUIT", cuit),
            ("Beneficio", beneficio),
            ("Documento", "12345678"),
            ("Código Categoría", "A")
        );

    private static Dictionary<string, string> FilaConsumo(string codigo) =>
        Fila(
            ("Entidad", "BDI"),
            ("Nro Socio", "10"),
            ("Código Consumo", codigo),
            ("Cuotas Pendientes", "1"),
            ("Monto Deuda", "100"),
            ("Concepto Descuento", "")
        );

    private static Dictionary<string, string> FilaServicio(string nroSocio, string codigo, string cuit = "", string beneficio = "") =>
        Fila(
            ("Entidad", "BDI"),
            ("Nro de Socio", nroSocio),
            ("Código Consumo", codigo),
            ("CUIT", cuit),
            ("Nro Beneficio", beneficio),
            ("Importe Cuota", "200"),
            ("Concepto Descuento", "")
        );

    // ── Tests válidos ─────────────────────────────────────────────────────────

    [Fact]
    public void Apply_ServicioValido_CodigoNoEstaEnConsumos_SeAcepta()
    {
        var padron = new List<Dictionary<string, string>> { FilaPadron("10") };
        var consumos = new List<Dictionary<string, string>> { FilaConsumo("9001") };
        var servicios = new List<Dictionary<string, string>> { FilaServicio("10", "8001") };

        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = padron,
            DatosConsumosValidados = consumos,
            DatosServiciosValidados = servicios,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Single(result.DatosServiciosValidados);
    }

    [Fact]
    public void Apply_CodigoServicioYaExisteEnConsumos_SeRechaza()
    {
        var padron = new List<Dictionary<string, string>> { FilaPadron("10") };
        var consumos = new List<Dictionary<string, string>> { FilaConsumo("9001") };
        var servicios = new List<Dictionary<string, string>> { FilaServicio("10", "9001") };   // mismo código

        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = padron,
            DatosConsumosValidados = consumos,
            DatosServiciosValidados = servicios,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Empty(result.DatosServiciosValidados);
    }

    [Fact]
    public void Apply_NroSocioNoExisteEnPadron_SeRechaza()
    {
        var padron = new List<Dictionary<string, string>> { FilaPadron("10") };
        var servicios = new List<Dictionary<string, string>> { FilaServicio("99", "8001") };

        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = padron,
            DatosConsumosValidados = [],
            DatosServiciosValidados = servicios,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Empty(result.DatosServiciosValidados);
    }

    [Fact]
    public void Apply_CodigoServicioDuplicadoDentroDelArchivo_SoloSeAceptaElPrimero()
    {
        var padron = new List<Dictionary<string, string>> { FilaPadron("10"), FilaPadron("11") };
        padron[1]["Nro Socio"] = "11";
        var servicios = new List<Dictionary<string, string>>
        {
            FilaServicio("10", "8001"),
            FilaServicio("11", "8001")   // código duplicado en servicios
        };

        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = padron,
            DatosConsumosValidados = [],
            DatosServiciosValidados = servicios,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Single(result.DatosServiciosValidados);
    }

    [Fact]
    public void Apply_EntidadNoExisteEnReferencia_SeRechaza()
    {
        var padron = new List<Dictionary<string, string>> { FilaPadron("10") };
        var servicios = new List<Dictionary<string, string>>
        {
            Fila(("Entidad","DESCONOCIDA"), ("Nro de Socio","10"),
                 ("Código Consumo","8001"), ("CUIT",""), ("Nro Beneficio",""),
                 ("Importe Cuota","200"), ("Concepto Descuento",""))
        };

        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = padron,
            DatosConsumosValidados = [],
            DatosServiciosValidados = servicios,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConEntidad("BDI"));

        Assert.Empty(result.DatosServiciosValidados);
    }

    [Fact]
    public void Apply_SinServicios_NoHaceNada()
    {
        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = [],
            DatosConsumosValidados = [],
            DatosServiciosValidados = [],
            HasLoadedData = false
        };

        _sut.Apply(result, _log);

        Assert.Empty(result.DatosServiciosValidados);
        Assert.False(_log.HasErrors);
    }
}
