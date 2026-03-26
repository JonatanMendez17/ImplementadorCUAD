using ImplementadorCUAD.Models;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Data;
using System.Globalization;
using ImplementadorCUAD.Services.Common;
using ImplementadorCUAD.Services.Validation;

namespace ImplementadorCUAD.Services;

public sealed class PadronValidator(IAppDbContextFactory dbContextFactory) : RowValidatorBase
{
    private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

    public void Apply(
        ImplementationValidationResult result,
        IAppLogger log,
        ValidationReferenceData? snapshot = null,
        DbErrorPolicy dbErrorPolicy = DbErrorPolicy.ContinueWithWarnings)
    {
        if (result.DatosPadronValidados.Count == 0)
        {
            return;
        }

        var categoriasValidasCodigo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categoriasValidasNombre = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filaCategoria in result.DatosCategoriasValidadas)
        {
            if (RowValueReader.TryGetFirstValue(filaCategoria, out var codigo, "Codigo Categoria", "Código Categoría") &&
                !string.IsNullOrWhiteSpace(codigo))
            {
                categoriasValidasCodigo.Add(codigo.Trim());
            }

            if (RowValueReader.TryGetFirstValue(filaCategoria, out var nombre, "Categoria", "Categoría") &&
                !string.IsNullOrWhiteSpace(nombre))
            {
                categoriasValidasNombre.Add(nombre.Trim());
            }
        }

        var safeSnapshot = snapshot ?? ValidationReferenceData.Empty;
        var categoriasCuadPorEntidad = safeSnapshot.CategoriasCuadPorEntidad;
        var categoriasConCuotaSocial = safeSnapshot.CategoriasConCuotaSocial;
        foreach (var kvp in categoriasCuadPorEntidad)
        {
            var entidadRef = kvp.Key;
            var predeterminadas = kvp.Value.Count(c => c.EsPredeterminada);
            if (predeterminadas > 1)
            {
                log.Warn($"Categorias Socios: La entidad '{entidadRef}' tiene mas de una categoria predeterminada en la base.");
            }
        }

        var sociosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var socioCategoria = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var documentosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var beneficiosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var empleadoPorSocioDocumento = LoadEmpleadoLookup(
            result.DatosPadronValidados,
            log,
            out var lookupDisponible,
            dbErrorPolicy);

        var padronFiltrado = FilterValidRows(
            "Padron",
            result.DatosPadronValidados,
            log,
            (row, rowNumber) =>
            {
            var erroresFila = new List<string>();

            var entidad = RowValueReader.GetFirstValue(row, "Entidad");
            var nroSocio = RowValueReader.GetFirstValue(row, "Nro Socio");
            var codigoCategoria = RowValueReader.GetFirstValue(row, "Codigo Categoria", "Código Categoría");
            var nombreCategoriaPadron = RowValueReader.GetFirstValue(row, "Categoria", "Categoría");
            var documento = RowValueReader.GetFirstValue(row, "Documento");
            var beneficio = RowValueReader.GetFirstValue(row, "Beneficio");

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
                        erroresFila.Add($"La categoria '{codigoCategoria}' no existe en la base para la entidad '{entidadClave}'.");
                    }
                    else if (categoriasConCuotaSocial.Count > 0)
                    {
                        var keyCuota = $"{entidadClave}|{codigoNorm}";
                        if (!categoriasConCuotaSocial.Contains(keyCuota))
                        {
                            erroresFila.Add($"La categoria '{codigoCategoria}' de la entidad '{entidadClave}' no tiene código de cuota social vigente en la base.");
                        }
                    }
                }
                else
                {
                    erroresFila.Add($"La entidad '{entidad}' no tiene categorías definidas en la base.");
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

            if (!string.IsNullOrWhiteSpace(nroSocio) && !string.IsNullOrWhiteSpace(documento))
            {
                var socioNormalizado = nroSocio.Trim();
                var documentoNormalizado = documento.Trim();
                var cacheKey = $"{socioNormalizado}|{documentoNormalizado}";

                if (!long.TryParse(documentoNormalizado, NumberStyles.None, CultureInfo.InvariantCulture, out var documentoNumero) || documentoNumero <= 0)
                {
                    erroresFila.Add($"El documento '{documento}' no es un numero valido para validar contra la base.");
                }
                else if (!lookupDisponible)
                {
                    erroresFila.Add("No hay conexión disponible a la base para validar Empleado/Persona.");
                }
                else
                {
                    if (!empleadoPorSocioDocumento.TryGetValue(cacheKey, out var value) || !value.Existe)
                    {
                        erroresFila.Add($"No existe empleado en la base para Nro Socio '{nroSocio}' y Documento '{documento}'.");
                    }
                }
            }

            return erroresFila;
            },
            out var rechazadas);

        if (rechazadas > 0)
        {
            log.Info($"Resumen validacion Padron socios: aceptadas={padronFiltrado.Count}, rechazadas={rechazadas}.");
        }

        result.DatosPadronValidados = padronFiltrado;
    }

    private Dictionary<string, (bool Existe, int EmrId)> LoadEmpleadoLookup(
        IReadOnlyList<Dictionary<string, string>> rows,
        IAppLogger log,
        out bool lookupDisponible,
        DbErrorPolicy dbErrorPolicy)
    {
        lookupDisponible = false;
        var pares = new List<(string EmpleadoCodigo, long Documento)>();
        foreach (var row in rows)
        {
            var nroSocio = RowValueReader.GetFirstValue(row, "Nro Socio")?.Trim();
            var documento = RowValueReader.GetFirstValue(row, "Documento")?.Trim();
            if (string.IsNullOrWhiteSpace(nroSocio) || string.IsNullOrWhiteSpace(documento))
            {
                continue;
            }

            if (!long.TryParse(documento, NumberStyles.None, CultureInfo.InvariantCulture, out var docNumero) || docNumero <= 0)
            {
                continue;
            }

            pares.Add((nroSocio, docNumero));
        }

        try
        {
            using var db = _dbContextFactory.Create();
            var lookup = db.GetEmrIdByEmpleadoCodigoYDocumentoBatch(pares);
            lookupDisponible = true;
            return lookup;
        }
        catch (Exception ex)
        {
            if (dbErrorPolicy == DbErrorPolicy.AbortValidation)
            {
                throw new DbValidationException(
                    "No se pudo consultar Empleado/Persona para validación de padrón.",
                    ex);
            }

            log.Error($"Padron socios: no se pudo abrir conexión a la base para validar Empleado/Persona. {ex.Message}");
            return new Dictionary<string, (bool Existe, int EmrId)>(StringComparer.OrdinalIgnoreCase);
        }
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

}

