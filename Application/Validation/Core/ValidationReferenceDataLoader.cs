using System.Globalization;
using ImplementadorCUAD.Data;
using ImplementadorCUAD.Infrastructure;

namespace ImplementadorCUAD.Services.Validation;

public sealed class ValidationReferenceDataLoader(IAppDbContextFactory dbContextFactory)
{
    private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

    public ValidationReferenceData Load()
    {
        using var db = _dbContextFactory.Create();

        var entidadesCuad = db.GetEntidad()
            .SelectMany(e => new[]
            {
                e.Nombre?.Trim(),
                e.EntId.ToString(CultureInfo.InvariantCulture)
            })
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var categoriasCuadPorEntidad = db.GetCategoriasCuad()
            .GroupBy(c => c.Entidad.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var catalogoPorEntidadServicio = db.GetCatalogoServiciosCuad()
            .ToDictionary(
                c => $"{c.Entidad.Trim()}|{c.Servicio.Trim()}",
                c => c,
                StringComparer.OrdinalIgnoreCase);

        return new ValidationReferenceData
        {
            EntidadesCuad = entidadesCuad,
            ConceptosDescuentoVigentes = db.GetConceptosDescuentoVigentesParaConsumos(),
            CategoriasCuadPorEntidad = categoriasCuadPorEntidad,
            CategoriasConCuotaSocial = db.GetCategoriasConCuotaSocialVigente(),
            CatalogoPorEntidadServicio = catalogoPorEntidadServicio
        };
    }
}
