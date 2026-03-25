using ImplementadorCUAD.Data;

namespace ImplementadorCUAD.Infrastructure;

public interface IAppDbContextFactory
{
    /// Crea un contexto contra la base (lecturas: Entidad, CategoriasCuad, etc.).
    IAppDbContext Create();

    /// Crea un contexto contra la base indicada (destino del empleador para importación/limpieza).
    /// No usar con null/vacío: el llamador debe validar antes.
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

