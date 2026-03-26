using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Models;
using ImplementadorCUAD.Services.Common;
using ImplementadorCUAD.Services.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace ImplementadorCUAD.Services
{
    public class FileImportService(IAppDbContextFactory dbContextFactory, ILogger<FileImportService>? logger = null)
    {
        private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;
        private readonly ILogger<FileImportService>? _logger = logger;
    private const DbErrorPolicy ValidationDbErrorPolicy = DbErrorPolicy.AbortValidation;

        public ImplementationValidationResult ValidateAndLoadFiles(ImplementationFileSelection selection, IAppLogger log, IProgress<int>? progress = null)
        {
            var result = new ImplementationValidationResult();

            // Los nombres lógicos que se pasan a LoadFile deben coincidir
            // con el atributo nombre de los nodos <Archivo nombre="..."> en Configuration.xml.
            var datosCategorias = string.IsNullOrWhiteSpace(selection.ArchivoCategorias)
                ? null
                : LoadFile("Categorias", selection.ArchivoCategorias, log, progress);

            var datosPadron = string.IsNullOrWhiteSpace(selection.ArchivoPadron)
                ? null
                : LoadFile("Padron", selection.ArchivoPadron, log, progress);

            var datosConsumos = string.IsNullOrWhiteSpace(selection.ArchivoConsumos)
                ? null
                : LoadFile("Consumos", selection.ArchivoConsumos, log, progress);

            List<Dictionary<string, string>>? datosConsumosDetalle = null;
            var archivosConsumosDetalle = selection.ArchivosConsumosDetalle;
            if (archivosConsumosDetalle != null && archivosConsumosDetalle.Count > 0)
            {
                datosConsumosDetalle = new List<Dictionary<string, string>>();
                var n = archivosConsumosDetalle.Count;
                for (var i = 0; i < n; i++)
                {
                    var ruta = archivosConsumosDetalle[i];
                    if (string.IsNullOrWhiteSpace(ruta)) continue;
                    if (n > 1)
                        log.Info($"ConsumosDetalle: cargando archivo {i + 1}/{n}: {Path.GetFileName(ruta)}");
                    var data = LoadFile("ConsumosDetalle", ruta, log, progress);
                    if (data != null && data.Count > 0)
                        datosConsumosDetalle.AddRange(data);
                }
                if (datosConsumosDetalle.Count == 0)
                    datosConsumosDetalle = null;
            }

            var datosServicios = string.IsNullOrWhiteSpace(selection.ArchivoServicios)
                ? null
                : LoadFile("Servicios", selection.ArchivoServicios, log, progress);

            var datosCatalogoServicios = string.IsNullOrWhiteSpace(selection.ArchivoCatalogoServicios)
                ? null
                : LoadFile("CatalogoServicios", selection.ArchivoCatalogoServicios, log, progress);

            if (datosPadron != null)
            {
                result.DatosPadronValidados = datosPadron;
                result.HasLoadedData = true;
            }

            if (datosCategorias != null)
            {
                result.DatosCategoriasValidadas = datosCategorias;
                result.HasLoadedData = true;
            }

            if (datosConsumos != null)
            {
                result.DatosConsumosValidados = datosConsumos;
                result.HasLoadedData = true;
            }

            if (datosConsumosDetalle != null)
            {
                result.DatosConsumosDetalleValidados = datosConsumosDetalle;
                result.HasLoadedData = true;
            }

            if (datosCatalogoServicios != null)
            {
                result.DatosCatalogoServiciosValidados = datosCatalogoServicios;
                result.HasLoadedData = true;
            }

            if (datosServicios != null)
            {
                result.DatosServiciosValidados = datosServicios;
                result.HasLoadedData = true;
            }

            var snapshot = LoadReferenceData(log, out var snapshotLoaded);
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
                log.Error($"Validación detenida por error de base en padrón. {ex.Message}");
                result.HasLoadedData = false;
                return result;
            }
            consumosValidator.Apply(result, log, snapshot);
            consumosDetalleValidator.Apply(result, log, snapshot);
            serviciosValidator.Apply(result, log, snapshot);
            catalogoServiciosValidator.Apply(result, log, snapshot);

            if (!result.HasLoadedData)
            {
                log.Error("No se pudo cargar ningun archivo.");
                _logger?.LogWarning("No se pudo cargar ningun archivo.");
            }

            return result;
        }

        private ValidationReferenceData LoadReferenceData(IAppLogger log, out bool loaded)
        {
            try
            {
                var loader = new ValidationReferenceDataLoader(_dbContextFactory);
                loaded = true;
                return loader.Load();
            }
            catch (Exception ex)
            {
                log.Error($"No se pudo cargar datos de referencia para validación. {ex.Message}");
                _logger?.LogError(ex, "No se pudo cargar datos de referencia para validación.");
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

            foreach (var line in File.ReadLines(filePath))
            {
                yield return line;
            }
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
                var indicePorEncabezadoNormalizado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < encabezados.Count; i++)
                {
                    var normalizado = NormalizeHeader(encabezados[i]);
                    if (!indicePorEncabezadoNormalizado.ContainsKey(normalizado))
                    {
                        indicePorEncabezadoNormalizado[normalizado] = i;
                    }
                }

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

                var registros = new List<Dictionary<string, string>>();
                var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var filasAceptadas = 0;
                var filasRechazadas = 0;
                var totalFilasDatos = 0;
                var filaNumero = 1;

                while (enumerator.MoveNext())
                {
                    filaNumero++;
                    var valores = enumerator.Current.Split(',');

                    // Si la row está completamente vacía (todas las columnas vacías o en blanco), se omite.
                    if (valores.All(v => string.IsNullOrWhiteSpace(v)))
                    {
                        continue;
                    }

                    var row = new Dictionary<string, string>();
                    var erroresFila = new List<string>();

                    for (int j = 0; j < columnasConfig.Count; j++)
                    {
                        var config = columnasConfig[j];
                        var indiceColumna = indiceColumnaPorClave[config.Clave];
                        var value = indiceColumna.HasValue && indiceColumna.Value < valores.Length
                            ? valores[indiceColumna.Value]
                            : string.Empty;

                        if (!ValidateGeneralRules(value, config, out var error))
                        {
                            erroresFila.Add($"columna '{config.Clave}': {error}");
                        }

                        row[config.Clave] = value;
                    }

                    var filaEsValida = erroresFila.Count == 0;

                    if (filaEsValida)
                    {
                        if (ValidateSpecificUniqueness(logicalName, filaNumero, row, uniqueKeys, log))
                        {
                            registros.Add(row);
                            filasAceptadas++;
                        }
                        else
                        {
                            filasRechazadas++;
                        }
                    }
                    else
                    {
                        log.Warn($"{logicalName} row {filaNumero}: {string.Join(" | ", erroresFila)}");
                        filasRechazadas++;
                    }

                    totalFilasDatos++;

                    if (totalFilasDatos % 1000 == 0 && progress is not null)
                    {
                        progress.Report(0);
                    }
                }

                log.Info($"{logicalName}: Validaciones realizadas correctamente.");
                log.Info($"Resumen {logicalName}: total={totalFilasDatos}, aceptadas={filasAceptadas}, rechazadas={filasRechazadas}.");
                return registros;
            }
            catch (Exception ex)
            {
                log.Error($"Error al cargar {logicalName}: {ex.Message}");
                _logger?.LogError(ex, "Error al cargar {LogicalName}", logicalName);
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

            var indicePorEncabezadoNormalizado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < encabezados.Count; i++)
            {
                var normalizado = NormalizeHeader(encabezados[i]);
                if (!indicePorEncabezadoNormalizado.ContainsKey(normalizado))
                {
                    indicePorEncabezadoNormalizado[normalizado] = i;
                }
            }

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

            var registros = new List<Dictionary<string, string>>();
            var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filasAceptadas = 0;
            var filasRechazadas = 0;
            var totalFilasDatos = 0;
            var filaNumero = 1;

            while (!parser.EndOfData)
            {
                filaNumero++;
                var fields = parser.ReadFields() ?? Array.Empty<string>();

                // Si la row está completamente vacía (todas las columnas vacías o en blanco), se omite.
                if (fields.All(v => string.IsNullOrWhiteSpace(v)))
                {
                    continue;
                }

                var row = new Dictionary<string, string>();
                var erroresFila = new List<string>();

                for (int j = 0; j < columnasConfig.Count; j++)
                {
                    var config = columnasConfig[j];
                    var indiceColumna = indiceColumnaPorClave[config.Clave];
                    var value = indiceColumna.HasValue && indiceColumna.Value < fields.Length
                        ? fields[indiceColumna.Value]
                        : string.Empty;

                    if (!ValidateGeneralRules(value, config, out var error))
                    {
                        erroresFila.Add($"columna '{config.Clave}': {error}");
                    }

                    row[config.Clave] = value;
                }

                var filaEsValida = erroresFila.Count == 0;

                if (filaEsValida)
                {
                    if (ValidateSpecificUniqueness(logicalName, filaNumero, row, uniqueKeys, log))
                    {
                        registros.Add(row);
                        filasAceptadas++;
                    }
                    else
                    {
                        filasRechazadas++;
                    }
                }
                else
                {
                    log.Warn($"{logicalName} row {filaNumero}: {string.Join(" | ", erroresFila)}");
                    filasRechazadas++;
                }

                totalFilasDatos++;

                if (totalFilasDatos % 1000 == 0 && progress is not null)
                {
                    progress.Report(0);
                }
            }

            log.Info($"{logicalName}: Validaciones realizadas correctamente.");
            log.Info($"Resumen {logicalName}: total={totalFilasDatos}, aceptadas={filasAceptadas}, rechazadas={filasRechazadas}.");
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
                return true;
            }

            if (texto.Length > config.LargoMaximo)
            {
                error = $"supera el largo maximo permitido ({config.LargoMaximo})";
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
                        error = "debe ser un numero entero sin letras";
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
                        error = "debe ser un value de dinero valido";
                        return false;
                    }

                    return true;

                case "date":
                    if (!ValueParsers.TryParseDateFlexible(texto, out _))
                    {
                        error = "debe ser una date valida";
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

        private static bool ValidateSpecificUniqueness(string logicalName, int rowNumber, Dictionary<string, string> row, HashSet<string> uniqueKeys, IAppLogger log)
        {
            if (logicalName.Equals("Padron", StringComparison.OrdinalIgnoreCase))
            {
                var nroSocio = RowValueReader.GetFirstValue(row, "Nro Socio");
                if (string.IsNullOrWhiteSpace(nroSocio))
                {
                    log.Warn($"Padron row {rowNumber}: 'Nro Socio' vacio.");
                    return false;
                }

                var clave = $"PADRON::{nroSocio.Trim()}";
                if (!uniqueKeys.Add(clave))
                {
                    log.Warn($"Padron row {rowNumber}: numero de socio '{nroSocio}' repetido.");
                    return false;
                }

                return true;
            }

            if (logicalName.Equals("Consumos", StringComparison.OrdinalIgnoreCase))
            {
                var nroConsumo = RowValueReader.GetFirstValue(row, "Codigo Consumo", "Código Consumo", "Codigo", "Código", "CÃ³digo");
                if (string.IsNullOrWhiteSpace(nroConsumo))
                {
                    log.Warn($"Consumos row {rowNumber}: codigo (nro de consumo) vacio.");
                    return false;
                }

                var clave = $"CONSUMOS::{nroConsumo.Trim()}";
                if (!uniqueKeys.Add(clave))
                {
                    log.Warn($"Consumos row {rowNumber}: nro de consumo '{nroConsumo}' repetido.");
                    return false;
                }

                return true;
            }

            return true;
        }

    }
}
