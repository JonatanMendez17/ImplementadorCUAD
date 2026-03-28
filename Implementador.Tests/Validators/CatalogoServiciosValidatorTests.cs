using Implementador.Application.Validation;
using Implementador.Application.Validation.Core;
using Implementador.Models;
using Implementador.Tests.Helpers;
using Xunit;

namespace Implementador.Tests.Validators;

public class CatalogoServiciosValidatorTests
{
    private readonly CatalogoServiciosValidator _sut = new();
    private readonly FakeLogger _log = new();

    private static Dictionary<string, string> Fila(params (string key, string value)[] pares) =>
        pares.ToDictionary(p => p.key, p => p.value);

    private static ValidationReferenceData SnapshotConCatalogo(string entidad, string servicio, decimal importe) =>
        new()
        {
            EntidadesRef = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entidad },
            CatalogoPorEntidadServicio = new Dictionary<string, CatalogoServicioRef>(StringComparer.OrdinalIgnoreCase)
            {
                [$"{entidad}|{servicio}"] = new CatalogoServicioRef
                {
                    Entidad = entidad,
                    Servicio = servicio,
                    Importe = importe,
                    Habilitado = true
                }
            }
        };

    private static Dictionary<string, string> FilaCatalogo(string entidad, string servicio, string importe) =>
        Fila(
            ("Entidad", entidad),
            ("Servicio", servicio),
            ("Importe", importe),
            ("Comentarios / Info de Servicio", "")
        );

    // ── Tests válidos ─────────────────────────────────────────────────────────

    [Fact]
    public void Apply_CatalogoValido_ImporteCoincide_SeAcepta()
    {
        var catalogo = new List<Dictionary<string, string>>
        {
            FilaCatalogo("BDI", "Consulta", "250.00")
        };
        var result = new ImplementationValidationResult
        {
            DatosCatalogoServiciosValidados = catalogo,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConCatalogo("BDI", "Consulta", 250.00m));

        Assert.Single(result.DatosCatalogoServiciosValidados);
    }

    [Fact]
    public void Apply_ImporteDentroDelMargenDeToleranciaPuntoCero1_SeAcepta()
    {
        var catalogo = new List<Dictionary<string, string>>
        {
            FilaCatalogo("BDI", "Consulta", "250.005")
        };
        var result = new ImplementationValidationResult
        {
            DatosCatalogoServiciosValidados = catalogo,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConCatalogo("BDI", "Consulta", 250.00m));

        Assert.Single(result.DatosCatalogoServiciosValidados);
    }

    [Fact]
    public void Apply_ImporteFueraDeToleranciaDe0_01_SeRechaza()
    {
        var catalogo = new List<Dictionary<string, string>>
        {
            FilaCatalogo("BDI", "Consulta", "251.00")   // difiere en 1.00
        };
        var result = new ImplementationValidationResult
        {
            DatosCatalogoServiciosValidados = catalogo,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConCatalogo("BDI", "Consulta", 250.00m));

        Assert.Empty(result.DatosCatalogoServiciosValidados);
    }

    [Fact]
    public void Apply_ServicioNoExisteEnCatalogo_SeRechaza()
    {
        var catalogo = new List<Dictionary<string, string>>
        {
            FilaCatalogo("BDI", "ServicioInexistente", "250.00")
        };
        var result = new ImplementationValidationResult
        {
            DatosCatalogoServiciosValidados = catalogo,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConCatalogo("BDI", "Consulta", 250.00m));

        Assert.Empty(result.DatosCatalogoServiciosValidados);
    }

    [Fact]
    public void Apply_EntidadVacia_SeRechaza()
    {
        var catalogo = new List<Dictionary<string, string>>
        {
            FilaCatalogo("", "Consulta", "250.00")
        };
        var result = new ImplementationValidationResult
        {
            DatosCatalogoServiciosValidados = catalogo,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConCatalogo("BDI", "Consulta", 250.00m));

        Assert.Empty(result.DatosCatalogoServiciosValidados);
    }

    [Fact]
    public void Apply_ImporteInvalido_SeRechaza()
    {
        var catalogo = new List<Dictionary<string, string>>
        {
            FilaCatalogo("BDI", "Consulta", "no-es-numero")
        };
        var result = new ImplementationValidationResult
        {
            DatosCatalogoServiciosValidados = catalogo,
            HasLoadedData = true
        };

        _sut.Apply(result, _log, SnapshotConCatalogo("BDI", "Consulta", 250.00m));

        Assert.Empty(result.DatosCatalogoServiciosValidados);
    }

    [Fact]
    public void Apply_SinCatalogo_NoHaceNada()
    {
        var result = new ImplementationValidationResult
        {
            DatosCatalogoServiciosValidados = [],
            HasLoadedData = false
        };

        _sut.Apply(result, _log);

        Assert.Empty(result.DatosCatalogoServiciosValidados);
        Assert.False(_log.HasErrors);
    }
}
