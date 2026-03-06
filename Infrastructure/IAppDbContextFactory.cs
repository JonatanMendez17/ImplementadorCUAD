using ImplementadorCUAD.Data;

namespace ImplementadorCUAD.Infrastructure;

public interface IAppDbContextFactory
{
    /// <summary>
    /// Crea un contexto contra la base CUAD (lecturas de referencia: Entidad, CategoriasCuad, etc.).
    /// </summary>
    IAppDbContext Create();

    /// <summary>
    /// Crea un contexto contra la base indicada (destino del empleador para importación/limpieza).
    /// No usar con null/vacío: el llamador debe validar antes.
    /// </summary>
    IAppDbContext Create(string? targetConnectionString);
}

public sealed class AppDbContextFactory : IAppDbContextFactory
{
    public IAppDbContext Create()
    {
        return new AppDbContext();
    }

    public IAppDbContext Create(string? targetConnectionString)
    {
        if (string.IsNullOrWhiteSpace(targetConnectionString))
            throw new ArgumentException("El connection string de destino no puede ser nulo ni vacío.", nameof(targetConnectionString));
        return new AppDbContext(targetConnectionString);
    }
}

