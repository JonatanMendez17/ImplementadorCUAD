using ExcelDataReader;
using MigradorCUAD.Models;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MigradorCUAD.Services
{
    public class FileImportService
    {
        public MigrationValidationResult ValidateAndLoadFiles(MigrationFileSelection selection, Action<string> log)
        {
            var result = new MigrationValidationResult();

            var datosCategorias = string.IsNullOrWhiteSpace(selection.ArchivoCategorias)
                ? null
                : LoadFile("Categorias", selection.ArchivoCategorias, log);

            var datosPadron = string.IsNullOrWhiteSpace(selection.ArchivoPadron)
                ? null
                : LoadFile("Padron", selection.ArchivoPadron, log);

            var datosConsumos = string.IsNullOrWhiteSpace(selection.ArchivoConsumos)
                ? null
                : LoadFile("Consumos", selection.ArchivoConsumos, log);

            var datosConsumosDetalle = string.IsNullOrWhiteSpace(selection.ArchivoConsumosDetalle)
                ? null
                : LoadFile("ConsumosDetalle", selection.ArchivoConsumosDetalle, log);

            var datosServicios = string.IsNullOrWhiteSpace(selection.ArchivoServicios)
                ? null
                : LoadFile("Servicios", selection.ArchivoServicios, log);

            var datosCatalogoServicios = string.IsNullOrWhiteSpace(selection.ArchivoCatalogoServicios)
                ? null
                : LoadFile("CatalogoServicios", selection.ArchivoCatalogoServicios, log);

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

            if (result.HuboCarga)
            {
                log("Archivos cargados con validaciones generales.");
            }
            else
            {
                log("No se pudo cargar ningún archivo.");
            }

            return result;
        }

        private string[] ReadFileLines(string rutaArchivo)
        {
            var extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();

            if (extension == ".csv" || extension == ".txt")
            {
                return File.ReadAllLines(rutaArchivo);
            }

            if (extension == ".xls" || extension == ".xlsx")
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                var filas = new List<string>();
                var builder = new StringBuilder();

                using var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                do
                {
                    while (reader.Read())
                    {
                        builder.Clear();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            if (i > 0)
                            {
                                builder.Append(',');
                            }

                            var valor = reader.GetValue(i)?.ToString() ?? string.Empty;
                            builder.Append(valor);
                        }

                        filas.Add(builder.ToString());
                    }
                } while (reader.NextResult());

                return filas.ToArray();
            }

            return File.ReadAllLines(rutaArchivo);
        }

        private List<Dictionary<string, string>>? LoadFile(string nombreLogico, string? rutaArchivo, Action<string> log)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rutaArchivo))
                {
                    return null;
                }

                if (!File.Exists(rutaArchivo))
                {
                    log($"Archivo inválido: {nombreLogico}");
                    return null;
                }

                var configService = new ConfiguracionService();
                var columnasConfig = configService.ObtenerColumnas(nombreLogico);
                if (columnasConfig.Count == 0)
                {
                    log($"No existe configuración XML para {nombreLogico}");
                    return null;
                }

                var lineas = ReadFileLines(rutaArchivo);
                if (lineas.Length == 0)
                {
                    log($"Archivo {nombreLogico} vacío.");
                    return null;
                }

                // Modo prueba: validaciones estructurales deshabilitadas.
                //var encabezado = lineas[0].Split(',');
                //if (encabezado.Length != columnasConfig.Count) return null;
                //for (int i = 0; i < encabezado.Length; i++)
                //{
                //    if (encabezado[i] != columnasConfig[i].Nombre) return null;
                //}

                var registros = new List<Dictionary<string, string>>();
                var clavesUnicas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lineas.Length; i++)
                {
                    var valores = lineas[i].Split(',');
                    var fila = new Dictionary<string, string>();
                    var filaEsValida = true;

                    for (int j = 0; j < columnasConfig.Count; j++)
                    {
                        var valor = j < valores.Length ? valores[j] : string.Empty;
                        var config = columnasConfig[j];

                        // Modo prueba: validación de tipo deshabilitada.
                        //var config = columnasConfig[j];
                        //if (!ValidateDataType(valor, config)) { ... }

                        if (!ValidateGeneralRules(valor, config, out var error))
                        {
                            log($"❌ {nombreLogico} - fila {i + 1}, columna '{config.Nombre}': {error}");
                            filaEsValida = false;
                        }

                        fila[config.Nombre] = valor;
                    }

                    if (filaEsValida)
                    {
                        if (ValidateSpecificUniqueness(nombreLogico, i + 1, fila, clavesUnicas, log))
                        {
                            registros.Add(fila);
                        }
                    }
                }

                log($"{nombreLogico} cargado con validaciones generales.");
                return registros;
            }
            catch (Exception ex)
            {
                log($"Error al cargar {nombreLogico}: {ex.Message}");
                return null;
            }
        }

        private static bool ValidateDataType(string valor, ColumnaConfiguracion config)
        {
            if (valor.Length > config.LargoMaximo)
            {
                return false;
            }

            switch (config.TipoDato.ToLower())
            {
                case "int":
                    return int.TryParse(valor, out _);

                case "decimal":
                    return decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

                case "fecha":
                    return DateTime.TryParse(valor, out _);

                case "texto":
                    return !string.IsNullOrWhiteSpace(valor);

                default:
                    return false;
            }
        }

        private static bool ValidateGeneralRules(string valor, ColumnaConfiguracion config, out string error)
        {
            error = string.Empty;
            var texto = valor?.Trim() ?? string.Empty;

            if (texto.Length == 0)
            {
                // Campos vacíos se aceptan en validación general.
                // La obligatoriedad se puede manejar en validaciones específicas.
                return true;
            }

            if (texto.Length > config.LargoMaximo)
            {
                error = $"supera el largo máximo permitido ({config.LargoMaximo})";
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
                        error = "debe ser un número entero sin letras";
                        return false;
                    }

                    if (numero <= 0)
                    {
                        error = "debe ser un número entero positivo";
                        return false;
                    }

                    return true;

                case "decimal":
                    if (!TryParseDecimalFlexible(texto, out _))
                    {
                        error = "debe ser un valor de dinero válido";
                        return false;
                    }

                    return true;

                case "fecha":
                    if (!DateTime.TryParse(texto, CultureInfo.GetCultureInfo("es-AR"), DateTimeStyles.None, out _) &&
                        !DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        error = "debe ser una fecha válida";
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

        private static bool HasWeirdCharacters(string texto)
        {
            // Permite letras (incluyendo acentos), dígitos, espacios y puntuación de uso común.
            return Regex.IsMatch(texto, @"[^\p{L}\p{N}\s\.\,\;\:\-\/\\(\)\'\""\#\%\&\+]");
        }

        private static bool ValidateSpecificUniqueness(
            string nombreLogico,
            int numeroFila,
            Dictionary<string, string> fila,
            HashSet<string> clavesUnicas,
            Action<string> log)
        {
            if (nombreLogico.Equals("Padron", StringComparison.OrdinalIgnoreCase))
            {
                if (!fila.TryGetValue("Nro Socio", out var nroSocio) || string.IsNullOrWhiteSpace(nroSocio))
                {
                    log($"❌ Padron - fila {numeroFila}: 'Nro Socio' vacío.");
                    return false;
                }

                var clave = $"PADRON::{nroSocio.Trim()}";
                if (!clavesUnicas.Add(clave))
                {
                    log($"❌ Padron - fila {numeroFila}: el número de socio '{nroSocio}' está repetido.");
                    return false;
                }

                return true;
            }

            if (nombreLogico.Equals("Consumos", StringComparison.OrdinalIgnoreCase))
            {
                if (!fila.TryGetValue("Código", out var nroConsumo) || string.IsNullOrWhiteSpace(nroConsumo))
                {
                    log($"❌ Consumos - fila {numeroFila}: 'Código' (nro de consumo) vacío.");
                    return false;
                }

                var clave = $"CONSUMOS::{nroConsumo.Trim()}";
                if (!clavesUnicas.Add(clave))
                {
                    log($"❌ Consumos - fila {numeroFila}: el nro de consumo '{nroConsumo}' está repetido.");
                    return false;
                }

                return true;
            }

            return true;
        }
    }
}
