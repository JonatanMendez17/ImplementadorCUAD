using ImplementadorCUAD.Models;
using ImplementadorCUAD.Infrastructure;

namespace ImplementadorCUAD.Services;

public sealed class PadronValidator(IAppDbContextFactory dbContextFactory)
{
    private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

    public void Apply(ImplementacionValidationResult result, Action<string> log)
    {
        if (result.DatosPadronValidados.Count == 0)
        {
            return;
        }

        var categoriasValidasCodigo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categoriasValidasNombre = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filaCategoria in result.DatosCategoriasValidadas)
        {
            if (TryGetFirstValue(filaCategoria, out var codigo, "Codigo Categoria", "Código Categoría") &&
                !string.IsNullOrWhiteSpace(codigo))
            {
                categoriasValidasCodigo.Add(codigo.Trim());
            }

            if (TryGetFirstValue(filaCategoria, out var nombre, "Categoria", "Categoría") &&
                !string.IsNullOrWhiteSpace(nombre))
            {
                categoriasValidasNombre.Add(nombre.Trim());
            }
        }

        Dictionary<string, List<CategoriaCuadRef>> categoriasCuadPorEntidad;
        HashSet<string> categoriasConCuotaSocial;
        try
        {
            using var db = _dbContextFactory.Create();
            categoriasCuadPorEntidad = db.GetCategoriasCuad()
                .GroupBy(c => c.Entidad.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            categoriasConCuotaSocial = db.GetCategoriasConCuotaSocialVigente();

            foreach (var kvp in categoriasCuadPorEntidad)
            {
                var entidadRef = kvp.Key;
                var predeterminadas = kvp.Value.Count(c => c.EsPredeterminada);
                if (predeterminadas > 1)
                {
                    log($"Categorias Socios: La entidad '{entidadRef}' tiene mas de una categoria predeterminada en CUAD.");
                }
            }
        }
        catch (Exception ex)
        {
            log($"Categorias Socios: No se pudo leer categorias de CUAD. {ex.Message}");
            categoriasCuadPorEntidad = new Dictionary<string, List<CategoriaCuadRef>>(StringComparer.OrdinalIgnoreCase);
            categoriasConCuotaSocial = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var padronFiltrado = new List<Dictionary<string, string>>();
        var sociosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var socioCategoria = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var documentosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var beneficiosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rechazadas = 0;

        for (int i = 0; i < result.DatosPadronValidados.Count; i++)
        {
            var fila = result.DatosPadronValidados[i];
            var numeroFila = i + 2;
            var erroresFila = new List<string>();

            var entidad = GetFirstValue(fila, "Entidad");
            var nroSocio = GetFirstValue(fila, "Nro Socio");
            var codigoCategoria = GetFirstValue(fila, "Codigo Categoria", "Código Categoría");
            var nombreCategoriaPadron = GetFirstValue(fila, "Categoria", "Categoría");
            var documento = GetFirstValue(fila, "Documento");
            var beneficio = GetFirstValue(fila, "Beneficio");

            if (string.IsNullOrWhiteSpace(nroSocio))
            {
                erroresFila.Add("El campo 'Nro Socio' se encuentra vacio.");
            }
            else
            {
                var nroSocioNormalizado = nroSocio.Trim();
                if (!sociosVistos.Add(nroSocioNormalizado))
                {
                    erroresFila.Add($"El numero de socio '{nroSocio}' se encuentra repetido.");
                }

                var categoriaNormalizada = (codigoCategoria ?? string.Empty).Trim();
                if (socioCategoria.TryGetValue(nroSocioNormalizado, out var categoriaExistente) &&
                    !string.Equals(categoriaExistente, categoriaNormalizada, StringComparison.OrdinalIgnoreCase))
                {
                    erroresFila.Add($"El socio '{nroSocio}' esta afiliado a mas de una categoria.");
                }
                else
                {
                    socioCategoria[nroSocioNormalizado] = categoriaNormalizada;
                }
            }

            if (!IsCategoriaValida(codigoCategoria, nombreCategoriaPadron, categoriasValidasCodigo, categoriasValidasNombre))
            {
                erroresFila.Add("La categoria informada no es valida.");
            }

            if (!string.IsNullOrWhiteSpace(entidad) && !string.IsNullOrWhiteSpace(codigoCategoria))
            {
                var entidadClave = entidad.Trim();
                if (categoriasCuadPorEntidad.TryGetValue(entidadClave, out var categoriasEntidad))
                {
                    var codigoNorm = codigoCategoria.Trim();
                    var categoriaCuad = categoriasEntidad
                        .FirstOrDefault(c =>
                            string.Equals(c.CodigoCategoria, codigoNorm, StringComparison.OrdinalIgnoreCase));

                    if (categoriaCuad == null)
                    {
                        erroresFila.Add($"La categoria '{codigoCategoria}' no existe en CUAD para la entidad '{entidadClave}'.");
                    }
                    else if (categoriasConCuotaSocial.Count > 0)
                    {
                        var keyCuota = $"{entidadClave}|{codigoNorm}";
                        if (!categoriasConCuotaSocial.Contains(keyCuota))
                        {
                            erroresFila.Add($"La categoria '{codigoCategoria}' de la entidad '{entidadClave}' no tiene código de cuota social vigente en CUAD.");
                        }
                    }
                }
                else
                {
                    erroresFila.Add($"La entidad '{entidad}' no tiene categorias definidas en CUAD.");
                }
            }

            if (!string.IsNullOrWhiteSpace(documento) && !documentosVistos.Add(documento.Trim()))
            {
                erroresFila.Add($"El documento '{documento}' se encuentra repetido.");
            }

            if (!string.IsNullOrWhiteSpace(beneficio) && !beneficiosVistos.Add(beneficio.Trim()))
            {
                erroresFila.Add($"El beneficio '{beneficio}' se encuentra repetido.");
            }

            if (erroresFila.Count == 0)
            {
                padronFiltrado.Add(fila);
            }
            else
            {
                rechazadas++;
                log($"Padron fila {numeroFila}: {string.Join(" | ", erroresFila)}");
            }
        }

        if (rechazadas > 0)
        {
            log($"Resumen validacion Padron socios: aceptadas={padronFiltrado.Count}, rechazadas={rechazadas}.");
        }

        result.DatosPadronValidados = padronFiltrado;
    }

    private static bool IsCategoriaValida( string? codigoCategoria, string? nombreCategoriaPadron, HashSet<string> categoriasValidasCodigo, HashSet<string> categoriasValidasNombre)
    {
        if (categoriasValidasCodigo.Count == 0 && categoriasValidasNombre.Count == 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(codigoCategoria) && categoriasValidasCodigo.Contains(codigoCategoria.Trim()))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(nombreCategoriaPadron) && categoriasValidasNombre.Contains(nombreCategoriaPadron.Trim()))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetFirstValue(Dictionary<string, string> fila, out string value, params string[] posiblesClaves)
    {
        foreach (var clave in posiblesClaves)
        {
            if (fila.TryGetValue(clave, out var encontrado))
            {
                value = encontrado;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static string GetFirstValue(Dictionary<string, string> fila, params string[] posiblesClaves)
    {
        return TryGetFirstValue(fila, out var value, posiblesClaves) ? value : string.Empty;
    }
}

