using Implementador.Models;
using Implementador.Infrastructure;
using Implementador.Data;
using System.Globalization;
using Implementador.Application.Validation.Common;
using Implementador.Application.Validation.Core;

namespace Implementador.Application.Validation;

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

        log.Separator();
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

        var categoriasDisponible = categoriasValidasCodigo.Count > 0 || categoriasValidasNombre.Count > 0;
        if (!categoriasDisponible)
        {
            log.Warn("Padron: no se cargó archivo de Categorías. No se puede verificar que el Codigo Categoría sea válido.");
        }

        var safeSnapshot = snapshot ?? ValidationReferenceData.Empty;
        var categoriasPorEntidadRef = safeSnapshot.CategoriasPorEntidadRef;
        var categoriasConCuotaSocial = safeSnapshot.CategoriasConCuotaSocial;
        foreach (var kvp in categoriasPorEntidadRef)
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
            ArchivoNombre.PadronSocios,
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

            var nroSocioNormalizado = nroSocio!.Trim();
            if (!sociosVistos.Add(nroSocioNormalizado))
            {
                erroresFila.Add($"El campo (Nro Socio) '{nroSocio}' se encuentra duplicado en el archivo.");
            }

            var categoriaNormalizada = (codigoCategoria ?? string.Empty).Trim();
            if (socioCategoria.TryGetValue(nroSocioNormalizado, out var categoriaExistente) &&
                !string.Equals(categoriaExistente, categoriaNormalizada, StringComparison.OrdinalIgnoreCase))
            {
                erroresFila.Add($"El campo (Nro Socio) '{nroSocio}' esta afiliado a mas de una categoria.");
            }
            else
            {
                socioCategoria[nroSocioNormalizado] = categoriaNormalizada;
            }

            if (categoriasDisponible && !IsCategoriaValida(codigoCategoria, nombreCategoriaPadron, categoriasValidasCodigo, categoriasValidasNombre))
            {
                erroresFila.Add("El campo (Categoria) no es valida.");
            }

            if (!string.IsNullOrWhiteSpace(entidad) && !string.IsNullOrWhiteSpace(codigoCategoria))
            {
                var entidadClave = entidad.Trim();
                if (categoriasPorEntidadRef.TryGetValue(entidadClave, out var categoriasEntidad))
                {
                    var codigoNorm = codigoCategoria.Trim();
                    var categoriaRef = categoriasEntidad
                        .FirstOrDefault(c =>
                            string.Equals(c.CodigoCategoria, codigoNorm, StringComparison.OrdinalIgnoreCase));

                    if (categoriaRef == null)
                    {
                        erroresFila.Add($"El campo (Codigo Categoria) '{codigoCategoria}' no existe en la base para la entidad '{entidadClave}'.");
                    }
                    else if (categoriasConCuotaSocial.Count > 0)
                    {
                        var keyCuota = $"{entidadClave}|{codigoNorm}";
                        if (!categoriasConCuotaSocial.Contains(keyCuota))
                        {
                            erroresFila.Add($"El campo (Codigo Categoria) '{codigoCategoria}' de la entidad '{entidadClave}' no tiene código de cuota social vigente en la base.");
                        }
                    }
                }
                else
                {
                    erroresFila.Add($"El campo (Entidad) '{entidad}' no tiene categorías definidas en la base.");
                }
            }

            if (!string.IsNullOrWhiteSpace(documento) && !documentosVistos.Add(documento.Trim()))
            {
                erroresFila.Add($"El campo (Documento) '{documento}' se encuentra duplicado en el archivo.");
            }

            if (!string.IsNullOrWhiteSpace(beneficio) && !beneficiosVistos.Add(beneficio.Trim()))
            {
                erroresFila.Add($"El campo (Beneficio) '{beneficio}' se encuentra duplicado en el archivo.");
            }

            if (!string.IsNullOrWhiteSpace(nroSocio) && !string.IsNullOrWhiteSpace(documento))
            {
                var socioNormalizado = nroSocio.Trim();
                var documentoNormalizado = documento.Trim();
                var cacheKey = $"{socioNormalizado}|{documentoNormalizado}";

                if (!long.TryParse(documentoNormalizado, NumberStyles.None, CultureInfo.InvariantCulture, out var documentoNumero) || documentoNumero <= 0)
                {
                    var soloDigitos = documentoNormalizado.All(char.IsDigit);
                    var esNotacionCientifica = documentoNormalizado.IndexOf('E', StringComparison.OrdinalIgnoreCase) >= 0;
                    var contieneLetras = documentoNormalizado.Any(char.IsLetter);
                    if (esNotacionCientifica || (soloDigitos && documentoNormalizado.Length > 18))
                        erroresFila.Add($"El campo (Documento) excede el limite de digitos permitidos.");
                    else if (contieneLetras)
                        erroresFila.Add($"El campo (Documento) no puede contener letras.");
                    else
                        erroresFila.Add($"El campo (Documento) no es un numero valido.");
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
            log.Info(ValidationLog.ReglaRechazadas(ArchivoNombre.PadronSocios, rechazadas, rechazadas + padronFiltrado.Count));
        log.Info(ValidationLog.ListasParaImplementar(ArchivoNombre.PadronSocios, padronFiltrado.Count));

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



