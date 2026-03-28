using Implementador.Application.Validation;
using Implementador.Data;
using Implementador.Infrastructure;
using Implementador.Models;
using Implementador.Tests.Helpers;
using Moq;
using Xunit;

namespace Implementador.Tests;

public class GeneralValidationServiceTests
{
    private readonly Mock<IAppDbContextFactory> _factoryMock = new();
    private readonly Mock<IAppDbContext> _dbMock = new();
    private readonly FakeLogger _log = new();

    public GeneralValidationServiceTests()
    {
        _factoryMock.Setup(f => f.Create()).Returns(_dbMock.Object);
        _factoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(_dbMock.Object);
    }

    private GeneralValidationService CrearSut() => new(_factoryMock.Object);

    private static Dictionary<string, string> Fila(string entidad) =>
        new() { ["Entidad"] = entidad };

    // ── ValidateEntidadConsistency ─────────────────────────────────────────────

    [Fact]
    public void ValidateEntidadConsistency_UnaEntidadEnTodosLosArchivos_RetornaTrue()
    {
        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = [Fila("BDI"), Fila("BDI")],
            DatosConsumosValidados = [Fila("BDI")],
            DatosConsumosDetalleValidados = [Fila("BDI")],
            DatosCategoriasValidadas = [Fila("BDI")],
            DatosServiciosValidados = [],
            DatosCatalogoServiciosValidados = [],
            HasLoadedData = true
        };

        var valido = CrearSut().ValidateEntidadConsistency(result, _log, out var entidad);

        Assert.True(valido);
        Assert.Equal("BDI", entidad);
    }

    [Fact]
    public void ValidateEntidadConsistency_MultipleEntidadesMezcladas_RetornaFalse()
    {
        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = [Fila("BDI")],
            DatosConsumosValidados = [Fila("ACTIVOS")],   // entidad diferente
            DatosConsumosDetalleValidados = [],
            DatosCategoriasValidadas = [],
            DatosServiciosValidados = [],
            DatosCatalogoServiciosValidados = [],
            HasLoadedData = true
        };

        var valido = CrearSut().ValidateEntidadConsistency(result, _log, out var entidad);

        Assert.False(valido);
        Assert.True(_log.HasErrors);
    }

    [Fact]
    public void ValidateEntidadConsistency_SinDatos_RetornaFalse()
    {
        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = [],
            DatosConsumosValidados = [],
            DatosConsumosDetalleValidados = [],
            DatosCategoriasValidadas = [],
            DatosServiciosValidados = [],
            DatosCatalogoServiciosValidados = [],
            HasLoadedData = false
        };

        var valido = CrearSut().ValidateEntidadConsistency(result, _log, out var entidad);

        Assert.False(valido);
        Assert.True(_log.HasErrors);
    }

    [Fact]
    public void ValidateEntidadConsistency_EntidadSoloenUnArchivo_RetornaTrue()
    {
        var result = new ImplementationValidationResult
        {
            DatosPadronValidados = [Fila("BDI")],
            DatosConsumosValidados = [],
            DatosConsumosDetalleValidados = [],
            DatosCategoriasValidadas = [],
            DatosServiciosValidados = [],
            DatosCatalogoServiciosValidados = [],
            HasLoadedData = true
        };

        var valido = CrearSut().ValidateEntidadConsistency(result, _log, out var entidad);

        Assert.True(valido);
        Assert.Equal("BDI", entidad);
    }

    // ── ValidateNoExistingDataForEntidad ───────────────────────────────────────

    [Fact]
    public void ValidateNoExistingDataForEntidad_NoHayDatosEnDB_RetornaTrue()
    {
        _dbMock.Setup(d => d.ExistsImportedDataForEntidad("BDI")).Returns(false);

        var empleador = new Empleador { Nombre = "Empleador Test" };
        var valido = CrearSut().ValidateNoExistingDataForEntidad(
            "BDI", empleador, "Server=test;", _log);

        Assert.True(valido);
    }

    [Fact]
    public void ValidateNoExistingDataForEntidad_YaExistenDatosEnDB_RetornaFalse()
    {
        _dbMock.Setup(d => d.ExistsImportedDataForEntidad("BDI")).Returns(true);

        var empleador = new Empleador { Nombre = "Empleador Test" };
        var valido = CrearSut().ValidateNoExistingDataForEntidad(
            "BDI", empleador, "Server=test;", _log);

        Assert.False(valido);
        Assert.True(_log.HasWarnings);
    }

    [Fact]
    public void ValidateNoExistingDataForEntidad_SinConnectionString_RetornaFalse()
    {
        var empleador = new Empleador { Nombre = "Empleador Test" };

        var valido = CrearSut().ValidateNoExistingDataForEntidad(
            "BDI", empleador, null, _log);

        Assert.False(valido);
        Assert.True(_log.HasErrors);
    }
}
