using Implementador.Data;
using Implementador.Infrastructure;

namespace Implementador.Application.Validation.Common;

/// <summary>
/// Verifica si valores de campos ya existen en la base del empleador destino.
/// Se construye con <see cref="Builder"/>, registrando un campo por llamada a <see cref="Builder.Add"/>.
/// </summary>
public sealed class DbDuplicateChecker
{
    private readonly Dictionary<string, HashSet<long>> _sets;

    private DbDuplicateChecker(Dictionary<string, HashSet<long>> sets) => _sets = sets;

    public static DbDuplicateChecker Vacio { get; } = new(new Dictionary<string, HashSet<long>>());

    public bool ExisteEnBase(string campo, long valor)
        => _sets.TryGetValue(campo, out var set) && set.Contains(valor);

    public sealed class Builder(IAppDbContextFactory factory, string? connectionString, IAppLogger log)
    {
        private readonly Dictionary<string, HashSet<long>> _sets = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registra un campo para verificar duplicados. El loader recibe el contexto de DB y retorna
        /// el conjunto de valores ya existentes. Se ignora si no hay connection string disponible.
        /// </summary>
        public Builder Add(string campo, Func<IAppDbContext, HashSet<long>> loader)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return this;

            try
            {
                using var db = factory.Create(connectionString);
                _sets[campo] = loader(db);
            }
            catch (Exception ex)
            {
                log.Warn($"No se pudo consultar la base para verificar duplicados del campo ({campo}). {ex.Message}");
            }

            return this;
        }

        public DbDuplicateChecker Build() => new(_sets);
    }
}
