using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Models;

namespace ImplementadorCUAD.Services
{
    public class FileImportService(IAppDbContextFactory dbContextFactory)
    {
        private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

        public ImplementacionValidationResult ValidateAndLoadFiles( ImplementacionFileSelection selection, Action<string> log, IProgress<int>? progress = null)
        {
            var result = new ImplementacionValidationResult();

            // Los nombres lógicos que se pasan a LoadFile deben coincidir
            // con el atributo nombre de los nodos <Archivo nombre="..."> en Configuracion.xml.
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
                        log($"ConsumosDetalle: cargando archivo {i + 1}/{n}: {Path.GetFileName(ruta)}");
                    var datos = LoadFile("ConsumosDetalle", ruta, log, progress);
                    if (datos != null && datos.Count > 0)
                        datosConsumosDetalle.AddRange(datos);
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
                result.HuboCarga = true;
            }

            if (datosCategorias != null)
            {
                result.DatosCategoriasValidadas = datosCategorias;
                result.HuboCarga = true;
            }

            if (datosConsumos != null)
            {
                result.DatosConsumosValidados = datosConsumos;
                result.HuboCarga = true;
            }

            if (datosConsumosDetalle != null)
            {
                result.DatosConsumosDetalleValidados = datosConsumosDetalle;
                result.HuboCarga = true;
            }

            if (datosCatalogoServicios != null)
            {
                result.DatosCatalogoServiciosValidados = datosCatalogoServicios;
                result.HuboCarga = true;
            }

            if (datosServicios != null)
            {
                result.DatosServiciosValidados = datosServicios;
                result.HuboCarga = true;
            }

            var padronValidator = new PadronValidator(_dbContextFactory);
            var consumosValidator = new ConsumosValidator(_dbContextFactory);
            var consumosDetalleValidator = new ConsumosDetalleValidator(_dbContextFactory);
            var serviciosValidator = new ServiciosValidator(_dbContextFactory);
            var catalogoServiciosValidator = new CatalogoServiciosValidator(_dbContextFactory);

            padronValidator.Apply(result, log);
            consumosValidator.Apply(result, log);
            consumosDetalleValidator.Apply(result, log);
            serviciosValidator.Apply(result, log);
            catalogoServiciosValidator.Apply(result, log);

            if (!result.HuboCarga)
            {
                log("No se pudo cargar ningun archivo.");
            }

            return result;
        }

        private IEnumerable<string> EnumerateFileLines(string rutaArchivo)
        {
            var extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();

            if (extension == ".csv" || extension == ".txt")
            {
                using var reader = new StreamReader(rutaArchivo);
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

                var filas = new List<string>();
                var builder = new StringBuilder();

                using var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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

                            string valor;
                            if (raw is IFormattable formattable && raw is not DateTime)
                            {
                                valor = formattable.ToString(null, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                valor = raw?.ToString() ?? string.Empty;
                            }

                            builder.Append(valor);
                        }

                        filas.Add(builder.ToString());
                    }
                } while (reader.NextResult());

                foreach (var l in filas)
                {
                    yield return l;
                }
                yield break;
            }

            foreach (var line in File.ReadLines(rutaArchivo))
            {
                yield return line;
            }
        }

        private List<Dictionary<string, string>>? LoadFile( string nombreLogico, string? rutaArchivo, Action<string> log, IProgress<int>? progress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rutaArchivo))
                {
                    return null;
                }

                if (!File.Exists(rutaArchivo))
                {
                    log($"Archivo invalido: {nombreLogico}");
                    return null;
                }

                var configService = new ConfiguracionService();
                var columnasConfig = configService.ObtenerColumnas(nombreLogico);
                if (columnasConfig.Count == 0)
                {
                    log($"No existe configuracion XML para {nombreLogico}");
                    return null;
                }

                using var enumerator = EnumerateFileLines(rutaArchivo).GetEnumerator();

                if (!enumerator.MoveNext())
                {
                    log($"El Archivo {nombreLogico} se encuentra vacio.");
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
                        log($"{nombreLogico}: Falta columna requerida para '{config.Clave}'.");
                        return null;
                    }
                }

                var registros = new List<Dictionary<string, string>>();
                var clavesUnicas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var filasAceptadas = 0;
                var filasRechazadas = 0;
                var totalFilasDatos = 0;
                var filaNumero = 1;

                while (enumerator.MoveNext())
                {
                    filaNumero++;
                    var valores = enumerator.Current.Split(',');

                    // Si la fila está completamente vacía (todas las columnas vacías o en blanco), se omite.
                    if (valores.All(v => string.IsNullOrWhiteSpace(v)))
                    {
                        continue;
                    }

                    var fila = new Dictionary<string, string>();
                    var erroresFila = new List<string>();

                    for (int j = 0; j < columnasConfig.Count; j++)
                    {
                        var config = columnasConfig[j];
                        var indiceColumna = indiceColumnaPorClave[config.Clave];
                        var valor = indiceColumna.HasValue && indiceColumna.Value < valores.Length
                            ? valores[indiceColumna.Value]
                            : string.Empty;

                        if (!ValidateGeneralRules(valor, config, out var error))
                        {
                            erroresFila.Add($"columna '{config.Clave}': {error}");
                        }

                        fila[config.Clave] = valor;
                    }

                    var filaEsValida = erroresFila.Count == 0;

                    if (filaEsValida)
                    {
                        if (ValidateSpecificUniqueness(nombreLogico, filaNumero, fila, clavesUnicas, log))
                        {
                            registros.Add(fila);
                            filasAceptadas++;
                        }
                        else
                        {
                            filasRechazadas++;
                        }
                    }
                    else
                    {
                        log($"{nombreLogico} fila {filaNumero}: {string.Join(" | ", erroresFila)}");
                        filasRechazadas++;
                    }

                    totalFilasDatos++;

                    if (totalFilasDatos % 1000 == 0 && progress is not null)
                    {
                        progress.Report(0);
                    }
                }

                log($"{nombreLogico}: Validaciones realizadas correctamente.");
                log($"Resumen {nombreLogico}: total={totalFilasDatos}, aceptadas={filasAceptadas}, rechazadas={filasRechazadas}.");
                return registros;
            }
            catch (Exception ex)
            {
                log($"Error al cargar {nombreLogico}: {ex.Message}");
                return null;
            }
        }

        private static int? ResolveColumnIndex( ColumnaConfiguracion config, Dictionary<string, int> indicePorEncabezadoNormalizado)
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

        private static string GetFirstValue(Dictionary<string, string> fila, params string[] posiblesClaves)
        {
            return TryGetFirstValue(fila, out var value, posiblesClaves) ? value : string.Empty;
        }

        private static bool ValidateGeneralRules(string valor, ColumnaConfiguracion config, out string error)
        {
            error = string.Empty;
            var texto = valor?.Trim() ?? string.Empty;

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
                    if (!TryParseDecimalFlexible(texto, out _))
                    {
                        error = "debe ser un valor de dinero valido";
                        return false;
                    }

                    return true;

                case "fecha":
                    if (!TryParseDateFlexible(texto, out _))
                    {
                        error = "debe ser una fecha valida";
                        return false;
                    }

                    return true;

                case "texto":
                    return true;

                default:
                    error = $"tipo de dato no soportado: {config.TipoDato}";
                    return false;
            }
        }

        private static bool TryParseDecimalFlexible(string texto, out decimal valor)
        {
            return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor) ||
                   decimal.TryParse(texto, NumberStyles.Number, CultureInfo.GetCultureInfo("es-AR"), out valor);
        }

        private static bool TryParseDateFlexible(string texto, out DateTime fecha)
        {
            return DateTime.TryParse(texto, CultureInfo.GetCultureInfo("es-AR"), DateTimeStyles.None, out fecha) ||
                   DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
        }

        private static bool HasWeirdCharacters(string texto)
        {
            return Regex.IsMatch(texto, @"[^\p{L}\p{N}\s\.\,\;\:\-\/\\\(\)\'\""\#\%\&\+]");
        }

        private static bool ValidateSpecificUniqueness( string nombreLogico, int numeroFila, Dictionary<string, string> fila, HashSet<string> clavesUnicas, Action<string> log)
        {
            if (nombreLogico.Equals("Padron", StringComparison.OrdinalIgnoreCase))
            {
                var nroSocio = GetFirstValue(fila, "Nro Socio");
                if (string.IsNullOrWhiteSpace(nroSocio))
                {
                    log($"Padron fila {numeroFila}: 'Nro Socio' vacio.");
                    return false;
                }

                var clave = $"PADRON::{nroSocio.Trim()}";
                if (!clavesUnicas.Add(clave))
                {
                    log($"Padron fila {numeroFila}: numero de socio '{nroSocio}' repetido.");
                    return false;
                }

                return true;
            }

            if (nombreLogico.Equals("Consumos", StringComparison.OrdinalIgnoreCase))
            {
                var nroConsumo = GetFirstValue(fila, "Codigo Consumo", "Código Consumo", "Codigo", "Código", "CÃ³digo");
                if (string.IsNullOrWhiteSpace(nroConsumo))
                {
                    log($"Consumos fila {numeroFila}: codigo (nro de consumo) vacio.");
                    return false;
                }

                var clave = $"CONSUMOS::{nroConsumo.Trim()}";
                if (!clavesUnicas.Add(clave))
                {
                    log($"Consumos fila {numeroFila}: nro de consumo '{nroConsumo}' repetido.");
                    return false;
                }

                return true;
            }

            return true;
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

    }
}
