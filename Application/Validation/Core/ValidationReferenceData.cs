using ImplementadorCUAD.Models;

namespace ImplementadorCUAD.Services.Validation;

public sealed class ValidationReferenceData
{
    public static ValidationReferenceData Empty { get; } = new();

    public HashSet<string> EntidadesCuad { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ConceptosDescuentoVigentes { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<CategoriaCuadRef>> CategoriasCuadPorEntidad { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CategoriasConCuotaSocial { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CatalogoServicioCuadRef> CatalogoPorEntidadServicio { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
