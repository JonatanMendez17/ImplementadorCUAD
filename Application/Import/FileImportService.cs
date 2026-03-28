using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using Implementador.Infrastructure;
using Implementador.Models;
using Implementador.Application.Configuration;
using Implementador.Application.Validation;
using Implementador.Application.Validation.Common;
using Implementador.Application.Validation.Core;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace Implementador.Application.Import
{
    public class FileImportService(IAppDbContextFactory dbContextFactory)
    {
        private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;
    private const DbErrorPolicy ValidationDbErrorPolicy = DbErrorPolicy.AbortValidation;

        public ImplementationValidationResult ValidateAndLoadFiles(ImplementationFileSelection selection, IAppLogger log, IProgress<int>? progress = null)
        {
            var result = new ImplementationValidationResult();

            // Los nombres lógicos que se pasan a LoadFiles deben coincidir
            // con el atributo nombre de los nodos <Archivo nombre="..."> en Configuration.xml.
            var datosCategorias    = LoadFiles("Categorias",      selection.ArchivosCategorias,      log, progress);
            var datosPadron        = LoadFiles("Padron",          selection.ArchivosPadron,          log, progress);
            var datosConsumos      = LoadFiles("Consumos",        selection.ArchivosConsumos,        log, progress);
            var datosConsumosDetalle = LoadFiles("ConsumosDetalle", selection.ArchivosConsumosDetalle, log, progress);
            var datosCatalogoServicios = LoadFiles("CatalogoServicios", selection.ArchivosCatalogoServicios, log, progress);
            var datosServicios     = LoadFiles("Servicios",       selection.ArchivosServicios,       log, progress);

            if (datosPadron != null)        { result.DatosPadronValidados             = datosPadron;            result.HasLoadedData = true; }
            if (datosCategorias != null)    { result.DatosCategoriasValidadas         = datosCategorias;        result.HasLoadedData = true; }
            if (datosConsumos != null)      { result.DatosConsumosValidados           = datosConsumos;          result.HasLoadedData = true; }
            if (datosConsumosDetalle != null) { result.DatosConsumosDetalleValidados  = datosConsumosDetalle;   result.HasLoadedData = true; }
            if (datosCatalogoServicios != null) { result.DatosCatalogoServiciosValidados = datosCatalogoServicios; result.HasLoadedData = true; }
            if (datosServicios != null)     { result.DatosServiciosValidados          = datosServicios;         result.HasLoadedData = true; }

            if (!string.IsNullOrWhiteSpace(selection.EntidadEsperada) && result.HasLoadedData)
            {
                var entidadDetectada = DetectarEntidadEnArchivos(result);
                if (!string.IsNullOrWhiteSpace(entidadDetectada) &&
                    !string.Equals(entidadDetectada, selection.EntidadEsperada.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    log.Error($"La entidad en los archivos ('{entidadDetectada}') no coincide con la entidad seleccionada ('{selection.EntidadEsperada}').");
                    result.HasLoadedData = false;
                    return result;
                }
            }

            ValidateOptionalServiceFilesAgainstSchema(result, log);
            var includeCatalogoServiciosRef = result.DatosCatalogoServiciosValidados.Count > 0;
            var snapshot = LoadReferenceData(log, includeCatalogoServiciosRef, out var snapshotLoaded);
            if (!snapshotLoaded && ValidationDbErrorPolicy == DbErrorPolicy.AbortValidation)
            {
                log.Error("Validación detenida: no se pudieron cargar datos de referencia de base.");
                result.DatosPadronValidados = [];
                result.DatosConsumosValidados = [];
                result.DatosConsumosDetalleValidados = [];
                result.DatosServiciosValidados = [];
                result.DatosCatalogoServiciosValidados = [];
                result.HasLoadedData = false;
                return result;
            }
            var padronValidator = new PadronValidator(_dbContextFactory);
            var consumosValidator = new ConsumosValidator(_dbContextFactory);
            var consumosDetalleValidator = new ConsumosDetalleValidator();
            var serviciosValidator = new ServiciosValidator();
            var catalogoServiciosValidator = new CatalogoServiciosValidator();

            try
            {
                padronValidator.Apply(result, log, snapshot, ValidationDbErrorPolicy);
            }
            catch (DbValidationException ex)
            {
                log.Error($"Validación detenida por error de base en padrón. {ex.Message} → {ex.InnerException?.Message}");
                result.HasLoadedData = false;
                return result;
            }
            consumosValidator.Apply(result, log, snapshot, selection.TargetConnectionString);
            consumosDetalleValidator.Apply(result, log, snapshot);
            serviciosValidator.Apply(result, log, snapshot);
            catalogoServiciosValidator.Apply(result, log, snapshot);

            if (!result.HasLoadedData)
            {
                log.Error("No se ha cargado ningún archivo para validar.");
            }

            return result;
        }

        private static string? DetectarEntidadEnArchivos(ImplementationValidationResult result)
        {
            var datasets = new[]
            {
                result.DatosPadronValidados,
                result.DatosCategoriasValidadas,
                result.DatosConsumosValidados,
                result.DatosConsumosDetalleValidados,
                result.DatosCatalogoServiciosValidados,
                result.DatosServiciosValidados,
            };

            foreach (var dataset in datasets)
            {
                var primera = dataset.FirstOrDefault();
                if (primera != null &&
                    primera.TryGetValue("Entidad", out var entidad) &&
                    !string.IsNullOrWhiteSpace(entidad))
                {
                    return entidad.Trim();
                }
            }

            return null;
        }

        private void ValidateOptionalServiceFilesAgainstSchema(ImplementationValidationResult result, IAppLogger log)
        {
            if (result.DatosCatalogoServiciosValidados.Count == 0 && result.DatosServiciosValidados.Count == 0)
            {
                return;
            }

            try
            {
                using var db = _dbContextFactory.Create();
                var faltaMutualCatalogo = !db.TableExists("Mutual_Catalogo");

                if (faltaMutualCatalogo && result.DatosCatalogoServiciosValidados.Count > 0)
                {
                    result.DatosCatalogoServiciosValidados = [];
                    log.Error("Catalogo Servicios: la base conectada no trabaja con este archivo. Falta la tabla Mutual_Catalogo.");
                }

                if (faltaMutualCatalogo && result.DatosServiciosValidados.Count > 0)
                {
                    result.DatosServiciosValidados = [];
                    log.Error("Consumos Servicios: la base conectada no trabaja con este archivo. Falta la tabla Mutual_Catalogo.");
                }
            }
            catch (Exception ex)
            {
                log.Error($"No se pudo validar estructura de tablas para archivos opcionales. {ex.Message}");
            }

            result.HasLoadedData =
                result.DatosPadronValidados.Count > 0 ||
                result.DatosCategoriasValidadas.Count > 0 ||
                result.DatosConsumosValidados.Count > 0 ||
                result.DatosConsumosDetalleValidados.Count > 0 ||
                result.DatosCatalogoServiciosValidados.Count > 0 ||
                result.DatosServiciosValidados.Count > 0;
        }

        private ValidationReferenceData LoadReferenceData(IAppLogger log, bool includeCatalogoServiciosRef, out bool loaded)
        {
            try
            {
                var loader = new ValidationReferenceDataLoader(_dbContextFactory);
                loaded = true;
                return loader.Load(includeCatalogoServiciosRef);
            }
            catch (Exception ex)
            {
                log.Error($"No se pudo cargar datos de referencia para validación. {ex.Message}");
                loaded = false;
                return ValidationReferenceData.Empty;
            }
        }

        private IEnumerable<string> EnumerateFileLines(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".csv" || extension == ".txt")
            {
                using var reader = new StreamReader(filePath);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
                yield break;
            }

            if (extension == ".xls" || extension == ".xlsx")
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                var rows = new List<string>();
                var builder = new StringBuilder();

                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                var rowIndex = 0;

                do
                {
                    while (reader.Read())
                    {
                        rowIndex++;
                        builder.Clear();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            if (i > 0)
                            {
                                builder.Append(',');
                            }

                            var raw = reader.GetValue(i);

                            string value;
                            if (raw is IFormattable formattable && raw is not DateTime)
                            {
                                value = formattable.ToString(null, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                value = raw?.ToString() ?? string.Empty;
                            }

                            builder.Append(value);
                        }

                        rows.Add(builder.ToString());
                    }
                } while (reader.NextResult());

                foreach (var l in rows)
                {
                    yield return l;
                }
                yield break;
            }

            throw new NotSupportedException($"El tipo de archivo '{extension}' no está soportado. Use .csv, .txt, .xls o .xlsx.");
        }

        private List<Dictionary<string, string>>? LoadFiles(string logicalName, IReadOnlyList<string>? rutas, IAppLogger log, IProgress<int>? progress)
        {
            if (rutas == null || rutas.Count == 0)
                return null;

            var result = new List<Dictionary<string, string>>();
            var n = rutas.Count;
            for (var i = 0; i < n; i++)
            {
                var ruta = rutas[i];
                if (string.IsNullOrWhiteSpace(ruta)) continue;
                if (n > 1)
                    log.Info($"{logicalName}: cargando archivo {i + 1}/{n}: {Path.GetFileName(ruta)}");
                var data = LoadFile(logicalName, ruta, log, progress);
                if (data != null && data.Count > 0)
                    result.AddRange(data);
            }

            return result.Count > 0 ? result : null;
        }

        private List<Dictionary<string, string>>? LoadFile(string logicalName, string? filePath, IAppLogger log, IProgress<int>? progress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return null;
                }

                if (!File.Exists(filePath))
                {
                    log.Error($"Archivo invalido: {logicalName}");
                    return null;
                }

                var configService = new ConfigurationService();
                var columnasConfig = configService.GetColumns(logicalName);
                if (columnasConfig.Count == 0)
                {
                    log.Error($"No existe configuracion XML para {logicalName}");
                    return null;
                }

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension == ".csv" || extension == ".txt")
                {
                    return LoadDelimitedFile(logicalName, filePath, columnasConfig, log, progress);
                }

                using var enumerator = EnumerateFileLines(filePath).GetEnumerator();

                if (!enumerator.MoveNext())
                {
                    log.Error($"El Archivo {logicalName} se encuentra vacio.");
                    return null;
                }

                var encabezados = enumerator.Current.Split(',')
                    .Select(v => v.Trim())
                    .ToList();
                var indicePorEncabezadoNormalizado = BuildNormalizedHeaderIndex(encabezados);
                var indiceColumnaPorClave = BuildConfiguredColumnIndexes(logicalName, columnasConfig, indicePorEncabezadoNormalizado, log);
                if (indiceColumnaPorClave is null)
                {
                    return null;
                }

                IEnumerable<string[]> EnumerateRawRows(IEnumerator<string> linesEnumerator)
                {
                    while (linesEnumerator.MoveNext())
                    {
                        yield return linesEnumerator.Current.Split(',');
                    }
                }

                return ProcessRows(
                    logicalName,
                    columnasConfig,
                    indiceColumnaPorClave,
                    EnumerateRawRows(enumerator),
                    firstDataRowNumber: 2,
                    log,
                    progress);
            }
            catch (Exception ex)
            {
                log.Error($"Error al cargar {logicalName}: {ex.Message}");
                return null;
            }
        }

        private static char DetectDelimiter(string filePath)
        {
            // Heurística: elegir el delimitador que genere más campos en el header.
            var commaCount = CountHeaderFields(filePath, ',');
            var semiCount = CountHeaderFields(filePath, ';');

            return semiCount > commaCount ? ';' : ',';
        }

        private static int CountHeaderFields(string filePath, char delimiter)
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var parser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };

            parser.SetDelimiters(delimiter.ToString());

            if (parser.EndOfData)
                return 0;

            var fields = parser.ReadFields();
            return fields?.Length ?? 0;
        }

        private List<Dictionary<string, string>>? LoadDelimitedFile(
            string logicalName,
            string filePath,
            List<ColumnConfiguration> columnasConfig,
            IAppLogger log,
            IProgress<int>? progress)
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var parser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };

            var delimiter = DetectDelimiter(filePath);
            parser.SetDelimiters(delimiter.ToString());

            if (parser.EndOfData)
            {
                log.Error($"El Archivo {logicalName} se encuentra vacio.");
                return null;
            }

            var headerFields = parser.ReadFields() ?? Array.Empty<string>();
            var encabezados = headerFields.Select(v => (v ?? string.Empty).Trim()).ToList();

            var indicePorEncabezadoNormalizado = BuildNormalizedHeaderIndex(encabezados);
            var indiceColumnaPorClave = BuildConfiguredColumnIndexes(logicalName, columnasConfig, indicePorEncabezadoNormalizado, log);
            if (indiceColumnaPorClave is null)
            {
                return null;
            }

            IEnumerable<string[]> EnumerateDelimitedRows()
            {
                while (!parser.EndOfData)
                {
                    yield return parser.ReadFields() ?? Array.Empty<string>();
                }
            }

            return ProcessRows(
                logicalName,
                columnasConfig,
                indiceColumnaPorClave,
                EnumerateDelimitedRows(),
                firstDataRowNumber: 2,
                log,
                progress);
        }

        private static Dictionary<string, int> BuildNormalizedHeaderIndex(IReadOnlyList<string> encabezados)
        {
            var indicePorEncabezadoNormalizado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < encabezados.Count; i++)
            {
                var normalizado = NormalizeHeader(encabezados[i]);
                if (!indicePorEncabezadoNormalizado.ContainsKey(normalizado))
                {
                    indicePorEncabezadoNormalizado[normalizado] = i;
                }
            }

            return indicePorEncabezadoNormalizado;
        }

        private static Dictionary<string, int?>? BuildConfiguredColumnIndexes(
            string logicalName,
            List<ColumnConfiguration> columnasConfig,
            Dictionary<string, int> indicePorEncabezadoNormalizado,
            IAppLogger log)
        {
            var indiceColumnaPorClave = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
            foreach (var config in columnasConfig)
            {
                var indice = ResolveColumnIndex(config, indicePorEncabezadoNormalizado);
                indiceColumnaPorClave[config.Clave] = indice;

                if (!indice.HasValue && config.Requerida)
                {
                    log.Error($"{logicalName}: Falta columna requerida para '{config.Clave}'.");
                    return null;
                }
            }

            return indiceColumnaPorClave;
        }

        private List<Dictionary<string, string>> ProcessRows(
            string logicalName,
            List<ColumnConfiguration> columnasConfig,
            Dictionary<string, int?> indiceColumnaPorClave,
            IEnumerable<string[]> rows,
            int firstDataRowNumber,
            IAppLogger log,
            IProgress<int>? progress)
        {
            var registros = new List<Dictionary<string, string>>();
            var filasAceptadas = 0;
            var filasRechazadas = 0;
            var totalFilasDatos = 0;
            var filaNumero = firstDataRowNumber - 1;

            foreach (var values in rows)
            {
                filaNumero++;

                // Si la row está completamente vacía (todas las columnas vacías o en blanco), se omite.
                if (values.All(v => string.IsNullOrWhiteSpace(v)))
                {
                    continue;
                }

                var row = new Dictionary<string, string>();
                var erroresFila = new List<string>();

                for (int j = 0; j < columnasConfig.Count; j++)
                {
                    var config = columnasConfig[j];
                    var indiceColumna = indiceColumnaPorClave[config.Clave];
                    var value = indiceColumna.HasValue && indiceColumna.Value < values.Length
                        ? values[indiceColumna.Value]
                        : string.Empty;

                    if (!ValidateGeneralRules(value, config, out var error))
                    {
                        erroresFila.Add($"El campo ({config.Clave}) {error}.");
                    }

                    row[config.Clave] = value;
                }

                if (erroresFila.Count == 0)
                {
                    registros.Add(row);
                    filasAceptadas++;
                }
                else
                {
                    log.Warn($"{logicalName} fila {filaNumero}: {string.Join(" | ", erroresFila)}");
                    filasRechazadas++;
                }

                totalFilasDatos++;

                if (totalFilasDatos % 1000 == 0 && progress is not null)
                {
                    progress.Report(0);
                }
            }

            if (totalFilasDatos == 0)
            {
                log.Warn($"{logicalName}: el archivo se encuentra vacio. No se cargaron registros.");
            }
            else if (filasRechazadas > 0)
            {
                log.Warn($"{logicalName}: {filasRechazadas} de {totalFilasDatos} filas rechazadas por formato o tipo de dato inválido.");
            }
            return registros;
        }

        private static int? ResolveColumnIndex( ColumnConfiguration config, Dictionary<string, int> indicePorEncabezadoNormalizado)
        {
            var candidatos = new List<string>();

            if (!string.IsNullOrWhiteSpace(config.Nombre))
            {
                candidatos.Add(config.Nombre);
            }

            if (config.Alias != null && config.Alias.Count > 0)
            {
                candidatos.AddRange(config.Alias.Where(a => !string.IsNullOrWhiteSpace(a)));
            }

            if (!string.IsNullOrWhiteSpace(config.Clave))
            {
                candidatos.Add(config.Clave);
            }

            foreach (var alias in candidatos.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var normalizado = NormalizeHeader(alias);
                if (indicePorEncabezadoNormalizado.TryGetValue(normalizado, out var indice))
                {
                    return indice;
                }
            }

            return null;
        }

        private static string NormalizeHeader(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var decomposed = input.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var ch in decomposed)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToUpperInvariant(ch));
                }
            }

            return builder.ToString();
        }

        private static bool ValidateGeneralRules(string value, ColumnConfiguration config, out string error)
        {
            error = string.Empty;
            var texto = value?.Trim() ?? string.Empty;

            if (texto.Length == 0)
            {
                if (config.Requerida)
                {
                    error = "se encuentra vacio";
                    return false;
                }

                return true;
            }

            if (texto.Length > config.LargoMaximo)
            {
                error = $"excede el largo maximo permitido ({config.LargoMaximo})";
                return false;
            }

            if (HasWeirdCharacters(texto))
            {
                error = "contiene caracteres extraños";
                return false;
            }

            switch (config.TipoDato.ToLowerInvariant())
            {
                case "int":
                    if (!int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var numero))
                    {
                        var soloDigitos = texto.All(char.IsDigit);
                        var esNotacionCientifica = texto.IndexOf('E', StringComparison.OrdinalIgnoreCase) >= 0;
                        if (esNotacionCientifica || (soloDigitos && texto.Length > 18))
                            error = "excede el limite de digitos permitidos";
                        else if (texto.Any(char.IsLetter))
                            error = "no puede contener letras";
                        else
                            error = "no es un numero valido";
                        return false;
                    }

                    if (numero <= 0)
                    {
                        error = "debe ser un numero entero positivo";
                        return false;
                    }

                    return true;

                case "decimal":
                    if (!ValueParsers.TryParseDecimalFlexible(texto, out _))
                    {
                        error = "no es un valor de dinero valido";
                        return false;
                    }

                    return true;

                case "date":
                    if (!ValueParsers.TryParseDateFlexible(texto, out _))
                    {
                        error = "no es una fecha valida";
                        return false;
                    }

                    return true;

                case "alpha":
                    if (texto.Any(char.IsDigit))
                    {
                        error = "no puede contener digitos";
                        return false;
                    }

                    return true;

                case "string":
                    return true;

                default:
                    error = $"tipo de dato no soportado: {config.TipoDato}";
                    return false;
            }
        }

        private static bool HasWeirdCharacters(string texto)
        {
            return Regex.IsMatch(texto, @"[^\p{L}\p{N}\s\.\,\;\:\-\/\\\(\)\'\""\#\%\&\+]");
        }


    }
}
