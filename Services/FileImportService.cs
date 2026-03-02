using ExcelDataReader;
using ImplementadorCUAD.Data;
using ImplementadorCUAD.Models;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ImplementadorCUAD.Services
{
    public class FileImportService
    {
        public ImplementacionValidationResult ValidateAndLoadFiles(ImplementacionFileSelection selection, Action<string> log)
        {
            var result = new ImplementacionValidationResult();

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

            ApplyPadronSpecificValidations(result, log);
            ApplyConsumosSpecificValidations(result, log);
            ApplyConsumosDetalleSpecificValidations(result, log);
            ApplyServiciosSpecificValidations(result, log);

            if (result.HuboCarga)
            {
                log("Archivos cargados con validaciones generales.");
            }
            else
            {
                log("No se pudo cargar ningun archivo.");
            }

            return result;
        }

        private static void ApplyPadronSpecificValidations(ImplementacionValidationResult result, Action<string> log)
        {
            if (result.DatosPadronValidados.Count == 0)
            {
                return;
            }

            var categoriasValidasCodigo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var categoriasValidasNombre = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filaCategoria in result.DatosCategoriasValidadas)
            {
                if (TryGetFirstValue(filaCategoria, out var codigo, "Codigo Categoria", "Código Categoría", "CÃ³digo CategorÃ­a") &&
                    !string.IsNullOrWhiteSpace(codigo))
                {
                    categoriasValidasCodigo.Add(codigo.Trim());
                }

                if (TryGetFirstValue(filaCategoria, out var nombre, "Categoria", "Categoría", "CategorÃ­a") &&
                    !string.IsNullOrWhiteSpace(nombre))
                {
                    categoriasValidasNombre.Add(nombre.Trim());
                }
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
                var filaValida = true;

                var nroSocio = GetFirstValue(fila, "Nro Socio");
                var codigoCategoria = GetFirstValue(fila, "Codigo Categoria", "Código Categoría", "CÃ³digo CategorÃ­a");
                var nombreCategoriaPadron = GetFirstValue(fila, "Categoria", "Categoría", "CategorÃ­a");
                var documento = GetFirstValue(fila, "Documento");
                var beneficio = GetFirstValue(fila, "Beneficio");

                if (string.IsNullOrWhiteSpace(nroSocio))
                {
                    log($"ERROR Padron fila {numeroFila}: 'Nro Socio' vacio.");
                    filaValida = false;
                }
                else
                {
                    var nroSocioNormalizado = nroSocio.Trim();
                    if (!sociosVistos.Add(nroSocioNormalizado))
                    {
                        log($"ERROR Padron fila {numeroFila}: numero de socio '{nroSocio}' repetido.");
                        filaValida = false;
                    }

                    var categoriaNormalizada = (codigoCategoria ?? string.Empty).Trim();
                    if (socioCategoria.TryGetValue(nroSocioNormalizado, out var categoriaExistente) &&
                        !string.Equals(categoriaExistente, categoriaNormalizada, StringComparison.OrdinalIgnoreCase))
                    {
                        log($"ERROR Padron fila {numeroFila}: socio '{nroSocio}' afiliado a mas de una categoria.");
                        filaValida = false;
                    }
                    else
                    {
                        socioCategoria[nroSocioNormalizado] = categoriaNormalizada;
                    }
                }

                if (!IsCategoriaValida(codigoCategoria, nombreCategoriaPadron, categoriasValidasCodigo, categoriasValidasNombre))
                {
                    log($"ERROR Padron fila {numeroFila}: categoria informada no valida.");
                    filaValida = false;
                }

                if (!string.IsNullOrWhiteSpace(documento) && !documentosVistos.Add(documento.Trim()))
                {
                    log($"ERROR Padron fila {numeroFila}: documento '{documento}' repetido.");
                    filaValida = false;
                }

                if (!string.IsNullOrWhiteSpace(beneficio) && !beneficiosVistos.Add(beneficio.Trim()))
                {
                    log($"ERROR Padron fila {numeroFila}: beneficio '{beneficio}' repetido.");
                    filaValida = false;
                }

                if (filaValida)
                {
                    padronFiltrado.Add(fila);
                }
                else
                {
                    rechazadas++;
                }
            }

            if (rechazadas > 0)
            {
                log($"Resumen validacion especifica Padron: aceptadas={padronFiltrado.Count}, rechazadas={rechazadas}.");
            }

            result.DatosPadronValidados = padronFiltrado;
        }

        private static void ApplyConsumosSpecificValidations(ImplementacionValidationResult result, Action<string> log)
        {
            if (result.DatosConsumosValidados.Count == 0)
            {
                return;
            }

            HashSet<string> entidadesCuad;
            try
            {
                using var db = new AppDbContext();
                entidadesCuad = db.GetEntidades()
                    .SelectMany(e => new[]
                    {
                        e.Nombre?.Trim(),
                        e.EntId.ToString(CultureInfo.InvariantCulture)
                    })
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                log($"ERROR Consumos: no se pudo validar entidades de CUAD. {ex.Message}");
                result.DatosConsumosValidados = new List<Dictionary<string, string>>();
                return;
            }

            var padronPorSocio = result.DatosPadronValidados
                .Where(f => TryGetFirstValue(f, out var nro, "Nro Socio") && !string.IsNullOrWhiteSpace(nro))
                .GroupBy(f => GetFirstValue(f, "Nro Socio").Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var consumosFiltrados = new List<Dictionary<string, string>>();
            var codigosConsumoVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rechazadas = 0;

            for (int i = 0; i < result.DatosConsumosValidados.Count; i++)
            {
                var fila = result.DatosConsumosValidados[i];
                var numeroFila = i + 2;
                var filaValida = true;

                var entidad = GetFirstValue(fila, "Entidad");
                var nroSocio = GetFirstValue(fila, "Nro Socio");
                var cuitConsumo = GetFirstValue(fila, "CUIT");
                var beneficioConsumo = GetFirstValue(fila, "Beneficio");
                var codigoConsumo = GetFirstValue(fila, "Codigo", "Código", "CÃ³digo");

                if (string.IsNullOrWhiteSpace(entidad) || !entidadesCuad.Contains(entidad.Trim()))
                {
                    log($"ERROR Consumos fila {numeroFila}: entidad '{entidad}' no existe en CUAD.");
                    filaValida = false;
                }

                if (string.IsNullOrWhiteSpace(nroSocio) || !padronPorSocio.TryGetValue(nroSocio.Trim(), out var filaPadron))
                {
                    log($"ERROR Consumos fila {numeroFila}: socio '{nroSocio}' no existe o no corresponde al padron.");
                    filaValida = false;
                }
                else
                {
                    var cuitPadron = GetFirstValue(filaPadron, "CUIT");
                    var beneficioPadron = GetFirstValue(filaPadron, "Beneficio");

                    if (!EqualsDigitsOnly(cuitConsumo, cuitPadron))
                    {
                        log($"ERROR Consumos fila {numeroFila}: CUIT no coincide con padron para socio '{nroSocio}'.");
                        filaValida = false;
                    }

                    if (!EqualsTrimmed(beneficioConsumo, beneficioPadron))
                    {
                        log($"ERROR Consumos fila {numeroFila}: Beneficio no coincide con padron para socio '{nroSocio}'.");
                        filaValida = false;
                    }
                }

                if (string.IsNullOrWhiteSpace(codigoConsumo))
                {
                    log($"ERROR Consumos fila {numeroFila}: codigo de consumo vacio.");
                    filaValida = false;
                }
                else if (!codigosConsumoVistos.Add(codigoConsumo.Trim()))
                {
                    log($"ERROR Consumos fila {numeroFila}: codigo de consumo '{codigoConsumo}' repetido.");
                    filaValida = false;
                }

                if (filaValida)
                {
                    consumosFiltrados.Add(fila);
                }
                else
                {
                    rechazadas++;
                }
            }

            if (rechazadas > 0)
            {
                log($"Resumen validacion especifica Consumos: aceptadas={consumosFiltrados.Count}, rechazadas={rechazadas}.");
            }

            result.DatosConsumosValidados = consumosFiltrados;
        }

        private static void ApplyConsumosDetalleSpecificValidations(ImplementacionValidationResult result, Action<string> log)
        {
            if (result.DatosConsumosDetalleValidados.Count == 0)
            {
                return;
            }

            HashSet<string> entidadesCuad;
            try
            {
                using var db = new AppDbContext();
                entidadesCuad = db.GetEntidades()
                    .SelectMany(e => new[]
                    {
                        e.Nombre?.Trim(),
                        e.EntId.ToString(CultureInfo.InvariantCulture)
                    })
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                log($"ERROR ConsumosDetalle: no se pudo validar entidades de CUAD. {ex.Message}");
                result.DatosConsumosDetalleValidados = new List<Dictionary<string, string>>();
                return;
            }

            var consumosPorCodigo = result.DatosConsumosValidados
                .Where(f => !string.IsNullOrWhiteSpace(GetFirstValue(f, "Codigo", "Código", "CÃ³digo")))
                .GroupBy(f => GetFirstValue(f, "Codigo", "Código", "CÃ³digo").Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var detalleFiltrado = new List<Dictionary<string, string>>();
            var rechazadas = 0;

            for (int i = 0; i < result.DatosConsumosDetalleValidados.Count; i++)
            {
                var fila = result.DatosConsumosDetalleValidados[i];
                var numeroFila = i + 2;
                var filaValida = true;

                var entidad = GetFirstValue(fila, "Entidad");
                var codigoConsumo = GetFirstValue(fila, "Codigo Consumo", "Código Consumo", "CÃ³digo Consumo");
                var fechaVencimientoText = GetFirstValue(fila, "Fecha Vencimiento");

                if (string.IsNullOrWhiteSpace(entidad) || !entidadesCuad.Contains(entidad.Trim()))
                {
                    log($"ERROR ConsumosDetalle fila {numeroFila}: entidad '{entidad}' no existe en CUAD.");
                    filaValida = false;
                }

                if (string.IsNullOrWhiteSpace(codigoConsumo) || !consumosPorCodigo.ContainsKey(codigoConsumo.Trim()))
                {
                    log($"ERROR ConsumosDetalle fila {numeroFila}: codigo de consumo '{codigoConsumo}' no existe en archivo de Consumos.");
                    filaValida = false;
                }

                if (!TryParseDateFlexible(fechaVencimientoText, out var fechaVencimiento))
                {
                    log($"ERROR ConsumosDetalle fila {numeroFila}: fecha de vencimiento invalida.");
                    filaValida = false;
                }
                else if (fechaVencimiento.Date <= DateTime.Today)
                {
                    log($"ERROR ConsumosDetalle fila {numeroFila}: fecha de vencimiento no puede ser hoy o anterior.");
                    filaValida = false;
                }

                if (filaValida)
                {
                    detalleFiltrado.Add(fila);
                }
                else
                {
                    rechazadas++;
                }
            }

            var detallePorCodigo = detalleFiltrado
                .Where(f => !string.IsNullOrWhiteSpace(GetFirstValue(f, "Codigo Consumo", "Código Consumo", "CÃ³digo Consumo")))
                .GroupBy(f => GetFirstValue(f, "Codigo Consumo", "Código Consumo", "CÃ³digo Consumo").Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var codigosInvalidosPorTotales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in detallePorCodigo)
            {
                var codigo = kvp.Key;
                var filasDetalle = kvp.Value;

                if (!consumosPorCodigo.TryGetValue(codigo, out var consumoFila))
                {
                    codigosInvalidosPorTotales.Add(codigo);
                    continue;
                }

                var cuotasPendientesText = GetFirstValue(consumoFila, "Cuotas Pendientes");
                var montoDeudaText = GetFirstValue(consumoFila, "Monto Deuda");

                if (!int.TryParse(cuotasPendientesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cuotasEsperadas))
                {
                    codigosInvalidosPorTotales.Add(codigo);
                    log($"ERROR ConsumosDetalle: no se pudo leer 'Cuotas Pendientes' para codigo de consumo '{codigo}'.");
                    continue;
                }

                if (!TryParseDecimalFlexible(montoDeudaText, out var montoEsperado))
                {
                    codigosInvalidosPorTotales.Add(codigo);
                    log($"ERROR ConsumosDetalle: no se pudo leer 'Monto Deuda' para codigo de consumo '{codigo}'.");
                    continue;
                }

                var cuotasDetalle = filasDetalle.Count;
                var sumaDetalle = 0m;
                var parseOk = true;
                foreach (var filaDetalle in filasDetalle)
                {
                    var montoText = GetFirstValue(filaDetalle, "Monto");
                    if (!TryParseDecimalFlexible(montoText, out var montoCuota))
                    {
                        parseOk = false;
                        break;
                    }

                    sumaDetalle += montoCuota;
                }

                if (!parseOk)
                {
                    codigosInvalidosPorTotales.Add(codigo);
                    log($"ERROR ConsumosDetalle: monto invalido en detalle para codigo de consumo '{codigo}'.");
                    continue;
                }

                var sumaCoincide = Math.Abs(sumaDetalle - montoEsperado) <= 0.01m;
                if (cuotasDetalle != cuotasEsperadas || !sumaCoincide)
                {
                    codigosInvalidosPorTotales.Add(codigo);
                    log($"ERROR ConsumosDetalle: cuotas/importe no coinciden para codigo '{codigo}'. Esperado cuotas={cuotasEsperadas}, monto={montoEsperado}. Detalle cuotas={cuotasDetalle}, monto={sumaDetalle}.");
                }
            }

            if (codigosInvalidosPorTotales.Count > 0)
            {
                var depurado = new List<Dictionary<string, string>>();
                foreach (var fila in detalleFiltrado)
                {
                    var codigo = GetFirstValue(fila, "Codigo Consumo", "Código Consumo", "CÃ³digo Consumo").Trim();
                    if (codigosInvalidosPorTotales.Contains(codigo))
                    {
                        rechazadas++;
                        continue;
                    }

                    depurado.Add(fila);
                }

                detalleFiltrado = depurado;
            }

            if (rechazadas > 0)
            {
                log($"Resumen validacion especifica ConsumosDetalle: aceptadas={detalleFiltrado.Count}, rechazadas={rechazadas}.");
            }

            result.DatosConsumosDetalleValidados = detalleFiltrado;
        }

        private static void ApplyServiciosSpecificValidations(ImplementacionValidationResult result, Action<string> log)
        {
            if (result.DatosServiciosValidados.Count == 0)
            {
                return;
            }

            HashSet<string> entidadesCuad;
            try
            {
                using var db = new AppDbContext();
                entidadesCuad = db.GetEntidades()
                    .SelectMany(e => new[]
                    {
                        e.Nombre?.Trim(),
                        e.EntId.ToString(CultureInfo.InvariantCulture)
                    })
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                log($"ERROR Servicios: no se pudo validar entidades de CUAD. {ex.Message}");
                result.DatosServiciosValidados = new List<Dictionary<string, string>>();
                return;
            }

            var padronPorSocio = result.DatosPadronValidados
                .Where(f => TryGetFirstValue(f, out var nro, "Nro Socio") && !string.IsNullOrWhiteSpace(nro))
                .GroupBy(f => GetFirstValue(f, "Nro Socio").Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var codigosConsumos = result.DatosConsumosValidados
                .Select(f => GetFirstValue(f, "Codigo", "Código", "CÃ³digo").Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var serviciosFiltrados = new List<Dictionary<string, string>>();
            var codigosServiciosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rechazadas = 0;

            for (int i = 0; i < result.DatosServiciosValidados.Count; i++)
            {
                var fila = result.DatosServiciosValidados[i];
                var numeroFila = i + 2;
                var filaValida = true;

                var entidad = GetFirstValue(fila, "Entidad");
                var nroSocio = GetFirstValue(fila, "Nro de Socio", "Nro Socio");
                var cuitServicio = GetFirstValue(fila, "CUIT");
                var beneficioServicio = GetFirstValue(fila, "Nro Beneficio", "Beneficio");
                var codigoConsumo = GetFirstValue(fila, "Codigo Consumo", "Código Consumo", "CÃ³digo Consumo");

                if (string.IsNullOrWhiteSpace(entidad) || !entidadesCuad.Contains(entidad.Trim()))
                {
                    log($"ERROR Servicios fila {numeroFila}: entidad '{entidad}' no existe en CUAD.");
                    filaValida = false;
                }

                if (string.IsNullOrWhiteSpace(nroSocio) || !padronPorSocio.TryGetValue(nroSocio.Trim(), out var filaPadron))
                {
                    log($"ERROR Servicios fila {numeroFila}: socio '{nroSocio}' no existe o no corresponde al padron.");
                    filaValida = false;
                }
                else
                {
                    var cuitPadron = GetFirstValue(filaPadron, "CUIT");
                    var beneficioPadron = GetFirstValue(filaPadron, "Beneficio");

                    if (!EqualsDigitsOnly(cuitServicio, cuitPadron))
                    {
                        log($"ERROR Servicios fila {numeroFila}: CUIT no coincide con padron para socio '{nroSocio}'.");
                        filaValida = false;
                    }

                    if (!EqualsTrimmed(beneficioServicio, beneficioPadron))
                    {
                        log($"ERROR Servicios fila {numeroFila}: Beneficio no coincide con padron para socio '{nroSocio}'.");
                        filaValida = false;
                    }
                }

                if (string.IsNullOrWhiteSpace(codigoConsumo))
                {
                    log($"ERROR Servicios fila {numeroFila}: codigo de consumo vacio.");
                    filaValida = false;
                }
                else
                {
                    var codigoNormalizado = codigoConsumo.Trim();
                    if (!codigosServiciosVistos.Add(codigoNormalizado))
                    {
                        log($"ERROR Servicios fila {numeroFila}: codigo de consumo '{codigoConsumo}' repetido en Servicios.");
                        filaValida = false;
                    }

                    if (codigosConsumos.Contains(codigoNormalizado))
                    {
                        log($"ERROR Servicios fila {numeroFila}: codigo de consumo '{codigoConsumo}' ya existe en archivo Consumos.");
                        filaValida = false;
                    }
                }

                if (filaValida)
                {
                    serviciosFiltrados.Add(fila);
                }
                else
                {
                    rechazadas++;
                }
            }

            if (rechazadas > 0)
            {
                log($"Resumen validacion especifica Servicios: aceptadas={serviciosFiltrados.Count}, rechazadas={rechazadas}.");
            }

            result.DatosServiciosValidados = serviciosFiltrados;
        }

        private static bool IsCategoriaValida(
            string? codigoCategoria,
            string? nombreCategoriaPadron,
            HashSet<string> categoriasValidasCodigo,
            HashSet<string> categoriasValidasNombre)
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

                var lineas = ReadFileLines(rutaArchivo);
                if (lineas.Length == 0)
                {
                    log($"Archivo {nombreLogico} vacio.");
                    return null;
                }

                var encabezados = lineas[0].Split(',')
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

                    if (indice.HasValue)
                    {
                        var nombreColumnaDetectada = encabezados[indice.Value];
                        log($"Columna detectada en {nombreLogico}: '{config.Clave}' -> '{nombreColumnaDetectada}'.");
                        continue;
                    }

                    if (config.Requerida)
                    {
                        var aliasEsperados = (config.Alias?.Count > 0 ? config.Alias : new List<string> { config.Nombre });
                        log($"ERROR {nombreLogico}: falta columna requerida para '{config.Clave}'. Alias esperados: {string.Join(", ", aliasEsperados)}.");
                        return null;
                    }

                    log($"Aviso {nombreLogico}: no se detecto columna opcional '{config.Clave}'.");
                }

                var registros = new List<Dictionary<string, string>>();
                var clavesUnicas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var totalFilasDatos = Math.Max(0, lineas.Length - 1);
                var filasAceptadas = 0;
                var filasRechazadas = 0;

                for (int i = 1; i < lineas.Length; i++)
                {
                    var valores = lineas[i].Split(',');
                    var fila = new Dictionary<string, string>();
                    var filaEsValida = true;

                    for (int j = 0; j < columnasConfig.Count; j++)
                    {
                        var config = columnasConfig[j];
                        var indiceColumna = indiceColumnaPorClave[config.Clave];
                        var valor = indiceColumna.HasValue && indiceColumna.Value < valores.Length
                            ? valores[indiceColumna.Value]
                            : string.Empty;

                        if (!ValidateGeneralRules(valor, config, out var error))
                        {
                            log($"ERROR {nombreLogico} fila {i + 1}, columna '{config.Clave}': {error}");
                            filaEsValida = false;
                        }

                        fila[config.Clave] = valor;
                    }

                    if (filaEsValida)
                    {
                        if (ValidateSpecificUniqueness(nombreLogico, i + 1, fila, clavesUnicas, log))
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
                        filasRechazadas++;
                    }
                }

                log($"{nombreLogico} cargado con validaciones generales.");
                log($"Resumen {nombreLogico}: total={totalFilasDatos}, aceptadas={filasAceptadas}, rechazadas={filasRechazadas}.");
                return registros;
            }
            catch (Exception ex)
            {
                log($"Error al cargar {nombreLogico}: {ex.Message}");
                return null;
            }
        }

        private static int? ResolveColumnIndex(
            ColumnaConfiguracion config,
            Dictionary<string, int> indicePorEncabezadoNormalizado)
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
                error = "contiene caracteres extranos";
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

        private static bool ValidateSpecificUniqueness(
            string nombreLogico,
            int numeroFila,
            Dictionary<string, string> fila,
            HashSet<string> clavesUnicas,
            Action<string> log)
        {
            if (nombreLogico.Equals("Padron", StringComparison.OrdinalIgnoreCase))
            {
                var nroSocio = GetFirstValue(fila, "Nro Socio");
                if (string.IsNullOrWhiteSpace(nroSocio))
                {
                    log($"ERROR Padron fila {numeroFila}: 'Nro Socio' vacio.");
                    return false;
                }

                var clave = $"PADRON::{nroSocio.Trim()}";
                if (!clavesUnicas.Add(clave))
                {
                    log($"ERROR Padron fila {numeroFila}: numero de socio '{nroSocio}' repetido.");
                    return false;
                }

                return true;
            }

            if (nombreLogico.Equals("Consumos", StringComparison.OrdinalIgnoreCase))
            {
                var nroConsumo = GetFirstValue(fila, "Codigo", "Código", "CÃ³digo");
                if (string.IsNullOrWhiteSpace(nroConsumo))
                {
                    log($"ERROR Consumos fila {numeroFila}: codigo (nro de consumo) vacio.");
                    return false;
                }

                var clave = $"CONSUMOS::{nroConsumo.Trim()}";
                if (!clavesUnicas.Add(clave))
                {
                    log($"ERROR Consumos fila {numeroFila}: nro de consumo '{nroConsumo}' repetido.");
                    return false;
                }

                return true;
            }

            return true;
        }

        private static bool EqualsTrimmed(string? left, string? right)
        {
            var a = (left ?? string.Empty).Trim();
            var b = (right ?? string.Empty).Trim();
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EqualsDigitsOnly(string? left, string? right)
        {
            static string Digits(string? text) => new string((text ?? string.Empty).Where(char.IsDigit).ToArray());
            return string.Equals(Digits(left), Digits(right), StringComparison.Ordinal);
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
}
