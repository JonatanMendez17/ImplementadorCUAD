using ExcelDataReader;
using MigradorCUAD.Models;
using System.Globalization;
using System.IO;
using System.Text;

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
                log("Modo prueba: archivos cargados sin validaciones.");
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
                for (int i = 1; i < lineas.Length; i++)
                {
                    var valores = lineas[i].Split(',');
                    var fila = new Dictionary<string, string>();

                    for (int j = 0; j < columnasConfig.Count; j++)
                    {
                        var valor = j < valores.Length ? valores[j] : string.Empty;

                        // Modo prueba: validación de tipo deshabilitada.
                        //var config = columnasConfig[j];
                        //if (!ValidateDataType(valor, config)) { ... }
                        fila[columnasConfig[j].Nombre] = valor;
                    }

                    registros.Add(fila);
                }

                log($"{nombreLogico} cargado en modo prueba (sin validaciones).");
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
    }
}
