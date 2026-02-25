using Microsoft.Win32;
using MigradorCUAD.Commands;
using MigradorCUAD.Models;
using MigradorCUAD.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using MigradorCUAD.Data;
using ExcelDataReader;
using System.Text;
namespace MigradorCUAD.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // Privados
        private string? _empleadorSeleccionado;
        private string? _entidadSeleccionada;
        private string? _archivoCategorias;
        private string? _archivoPadron;
        private string? _archivoConsumos;
        private string? _archivoConsumosDetalle;
        private string? _archivoServicios;
        private string? _archivoCatalogoServicios;
        private int _progreso;
        private bool _estaProcesando;
        private bool _validacionFinalizada;
        private List<Dictionary<string, string>> _datosValidados = new();
        private List<Dictionary<string, string>> _datosCategoriasValidadas = new();
        private List<Dictionary<string, string>> _datosConsumosValidados = new();
        private List<Dictionary<string, string>> _datosConsumosDetalleValidados = new();
        private List<Dictionary<string, string>> _datosCatalogoServiciosValidados = new();
        private List<Dictionary<string, string>> _datosServiciosValidados = new();


        // Publicas
        public string? EmpleadorSeleccionado
        {
            get => _empleadorSeleccionado;
            set => SetProperty(ref _empleadorSeleccionado, value);
        }
        public string? EntidadSeleccionada
        {
            get => _entidadSeleccionada;
            set => SetProperty(ref _entidadSeleccionada, value);
        }
        public string? ArchivoCategorias
        {
            get => _archivoCategorias;
            set => SetProperty(ref _archivoCategorias, value);
        }
        public string? ArchivoPadron
        {
            get => _archivoPadron;
            set => SetProperty(ref _archivoPadron, value);
        }
        public string? ArchivoConsumos
        {
            get => _archivoConsumos;
            set => SetProperty(ref _archivoConsumos, value);

        }
        public string? ArchivoConsumosDetalle
        {
            get => _archivoConsumosDetalle;
            set => SetProperty(ref _archivoConsumosDetalle, value);

        }
        public string? ArchivoServicios
        {
            get => _archivoServicios;
            set => SetProperty(ref _archivoServicios, value);

        }
        public string? ArchivoCatalogoServicios
        {
            get => _archivoCatalogoServicios;
            set => SetProperty(ref _archivoCatalogoServicios, value);

        }
        public int Progreso
        {
            get => _progreso;
            set => SetProperty(ref _progreso, value);
        }
        public bool EstaProcesando
        {
            get => _estaProcesando;
            set => SetProperty(ref _estaProcesando, value);
        }
        public bool ValidacionFinalizada
        {
            get => _validacionFinalizada;
            set => SetProperty(ref _validacionFinalizada, value);
        }


        public ObservableCollection<string> Logs { get; set; }
        public ObservableCollection<Empleador> Empleador { get; set; }
        public ObservableCollection<Entidad> Entidades { get; set; }


        // Comandos
        public ICommand SeleccionarCategoriasCommand { get; }
        public ICommand SeleccionarPadronCommand { get; }
        public ICommand SeleccionarConsumosCommand { get; }
        public ICommand SeleccionarConsumosDetalleCommand { get; }
        public ICommand SeleccionarServiciosCommand { get; }
        public ICommand SeleccionarCatalogoServiciosCommand { get; }
        public ICommand ValidarCommand { get; }
        public ICommand CopiarABaseCommand {  get; }
        public ICommand CopiarCommand { get; }
        public ICommand? ExportarLogCommand { get; }
        public ICommand LimpiarPantallaCommand { get; }


        // Constructor
        public MainViewModel()
        {
            Logs = new ObservableCollection<string>();
            Progreso = 0;

            // Cargar datos de Empleadores y Entidades desde la base de datos
            using (var db = new AppDbContext())
            {
                var empleadorDb = db.GetEmpleadores();

                var entidadesDb = db.GetEntidades();

                Empleador = new ObservableCollection<Empleador>(empleadorDb);
                Entidades = new ObservableCollection<Entidad>(entidadesDb);
            }

            SeleccionarCategoriasCommand = new RelayCommand(_ => SeleccionarArchivo("Categorias"));
            SeleccionarPadronCommand = new RelayCommand(_ => SeleccionarArchivo("Padron"));
            SeleccionarConsumosCommand = new RelayCommand(_ => SeleccionarArchivo("Consumos"));
            SeleccionarConsumosDetalleCommand = new RelayCommand(_ => SeleccionarArchivo("ConsumosDetalle"));
            SeleccionarServiciosCommand = new RelayCommand(_ => SeleccionarArchivo("Servicios"));
            SeleccionarCatalogoServiciosCommand = new RelayCommand(_ => SeleccionarArchivo("CatalogoServicios"));

            ValidarCommand = new RelayCommand(_ => ValidarArchivos());

            CopiarABaseCommand = new RelayCommand(CopiarABase, PuedeCopiarABase);

            CopiarCommand = new SimpleAsyncCommand(CopiarABaseAsync);

            LimpiarPantallaCommand = new RelayCommand(_ => LimpiarPantalla());

        }


        //Metodos privados
        private string[] LeerLineasArchivo(string rutaArchivo)
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

                using (var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        while (reader.Read())
                        {
                            builder.Clear();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                if (i > 0)
                                    builder.Append(',');

                                var valor = reader.GetValue(i)?.ToString() ?? string.Empty;
                                builder.Append(valor);
                            }

                            filas.Add(builder.ToString());
                        }
                    } while (reader.NextResult());
                }

                return filas.ToArray();
            }

            // Por defecto, intentar leer como texto plano
            return File.ReadAllLines(rutaArchivo);
        }
        private void SeleccionarArchivo(string tipo)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Archivos Excel (*.xls;*.xlsx)|*.xls;*.xlsx|Archivos CSV (*.csv)|*.csv|Archivos TXT (*.txt)|*.txt|Todos los archivos (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                switch (tipo)
                {
                    case "Categorias":
                        ArchivoCategorias = dialog.FileName;
                        break;

                    case "Padron":
                        ArchivoPadron = dialog.FileName;
                        break;

                    case "Consumos":
                        ArchivoConsumos = dialog.FileName;
                        break;

                    case "ConsumosDetalle":
                        ArchivoConsumosDetalle = dialog.FileName;
                        break;

                    case "Servicios":
                        ArchivoServicios = dialog.FileName;
                        break;

                    case "CatalogoServicios":
                        ArchivoCatalogoServicios = dialog.FileName;
                        break;
                }
            }
        }

        // Método principal de validación   
        private void ValidarArchivos()
        {
            Logs.Clear();
            // Modo prueba: validaciones deshabilitadas para permitir carga directa.
            //if (EmpleadorSeleccionado == null) { ... }
            //if (EntidadSeleccionada == null) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoCategorias)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoPadron)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoConsumos)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoConsumosDetalle)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoServicios)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoCatalogoServicios)) { ... }
            var datosCategorias = string.IsNullOrWhiteSpace(ArchivoCategorias) ? null : ValidarArchivo("Categorias", ArchivoCategorias);
            var datosPadron = string.IsNullOrWhiteSpace(ArchivoPadron) ? null : ValidarArchivo("Padron", ArchivoPadron);
            var datosConsumos = string.IsNullOrWhiteSpace(ArchivoConsumos) ? null : ValidarArchivo("Consumos", ArchivoConsumos);
            var datosConsumosDetalle = string.IsNullOrWhiteSpace(ArchivoConsumosDetalle) ? null : ValidarArchivo("ConsumosDetalle", ArchivoConsumosDetalle);
            var datosServicios = string.IsNullOrWhiteSpace(ArchivoServicios) ? null : ValidarArchivo("Servicios", ArchivoServicios);
            var datosCatalogoServicios = string.IsNullOrWhiteSpace(ArchivoCatalogoServicios) ? null : ValidarArchivo("CatalogoServicios", ArchivoCatalogoServicios);

            var huboCarga = false;

            if (datosPadron != null)
            {
                _datosValidados = datosPadron;
                huboCarga = true;
            }

            if (datosCategorias != null)
            {
                _datosCategoriasValidadas = datosCategorias;
                huboCarga = true;
            }

            if (datosConsumos != null)
            {
                _datosConsumosValidados = datosConsumos;
                huboCarga = true;
            }

            if (datosConsumosDetalle != null)
            {
                _datosConsumosDetalleValidados = datosConsumosDetalle;
                huboCarga = true;
            }

            if (datosCatalogoServicios != null)
            {
                _datosCatalogoServiciosValidados = datosCatalogoServicios;
                huboCarga = true;
            }

            if (datosServicios != null)
            {
                _datosServiciosValidados = datosServicios;
                huboCarga = true;
            }

            if (huboCarga)
            {
                Logs.Add("Modo prueba: archivos cargados sin validaciones.");
            }
            else
            {
                Logs.Add("No se pudo cargar ningún archivo.");
            }

            ValidacionFinalizada = huboCarga;
        }
        // Validaci?n espec?fica del archivo de Categor?as
        private void ValidarArchivoCategorias()
        {
            try
            {
                var configService = new ConfiguracionService();
                var columnasConfig = configService.ObtenerColumnas("Categorias");

                var lineas = LeerLineasArchivo(ArchivoCategorias!);

                if (lineas.Length == 0)
                {
                    Logs.Add("❌ Archivo vacío.");
                    return;
                }

                var encabezado = lineas[0].Split(',');

                // Validar columnas
                if (encabezado.Length != columnasConfig.Count)
                {
                    Logs.Add("❌ Cantidad de columnas incorrecta.");
                    return;
                }

                for (int i = 0; i < encabezado.Length; i++)
                {
                    if (encabezado[i] != columnasConfig[i].Nombre)
                    {
                        Logs.Add($"❌ Nombre de columna incorrecto: {encabezado[i]}");
                        return;
                    }
                }

                // Validar registros
                for (int i = 1; i < lineas.Length; i++)
                {
                    var valores = lineas[i].Split(',');

                    for (int j = 0; j < columnasConfig.Count; j++)
                    {
                        var valor = valores[j];
                        var config = columnasConfig[j];

                        if (!ValidarTipoDato(valor, config))
                        {
                            Logs.Add($"❌ Error en fila {i + 1}, columna {config.Nombre}");
                        }
                    }
                }

                Logs.Add("✅ Archivo Categorías validado correctamente.");
            }
            catch (Exception ex)
            {
                Logs.Add($"❌ Error: {ex.Message}");
            }
        }

        // Validación de tipo de dato individual según configuración
        private bool ValidarTipoDato(string valor, ColumnaConfiguracion config)
        {
            if (valor.Length > config.LargoMaximo)
                return false;

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

        private List<Dictionary<string, string>>? ValidarArchivo(string nombreLogico, string? rutaArchivo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rutaArchivo))
                {
                    return null;
                }

                if (!File.Exists(rutaArchivo))
                {
                    Logs.Add($"Archivo inv?lido: {nombreLogico}");
                    return null;
                }
                var configService = new ConfiguracionService();
                var columnasConfig = configService.ObtenerColumnas(nombreLogico);
                if (columnasConfig.Count == 0)
                {
                    Logs.Add($"No existe configuraci?n XML para {nombreLogico}");
                    return null;
                }
                var lineas = LeerLineasArchivo(rutaArchivo);
                if (lineas.Length == 0)
                {
                    Logs.Add($"Archivo {nombreLogico} vac?o.");
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
                        // Modo prueba: validaci?n de tipo deshabilitada.
                        //var config = columnasConfig[j];
                        //if (!ValidarTipoDato(valor, config)) { ... }
                        fila[columnasConfig[j].Nombre] = valor;
                    }
                    registros.Add(fila);
                }
                Logs.Add($"{nombreLogico} cargado en modo prueba (sin validaciones).");
                return registros;
            }
            catch (Exception ex)
            {
                Logs.Add($"Error al cargar {nombreLogico}: {ex.Message}");
                return null;
            }
        }
        private List<CategoriaSocio> MapearCategorias()
        {
            var resultado = new List<CategoriaSocio>();

            foreach (var fila in _datosCategoriasValidadas)
            {
                try
                {
                    // Lectura segura de valores
                    fila.TryGetValue("Entidad", out var entidad);
                    fila.TryGetValue("Código Categoría", out var codigoCategoria);
                    fila.TryGetValue("Categoría", out var nombreCategoria);
                    fila.TryGetValue("Descripción Categoría", out var descripcionCategoria);
                    fila.TryGetValue("Categoría Predeterminada", out var esPredeterminadaTexto);
                    fila.TryGetValue("Monto CS", out var montoCsTexto);
                    fila.TryGetValue("Concepto Descuento", out var conceptoDescuentoTexto);

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(codigoCategoria) ||
                        string.IsNullOrWhiteSpace(nombreCategoria) ||
                        string.IsNullOrWhiteSpace(montoCsTexto) ||
                        string.IsNullOrWhiteSpace(conceptoDescuentoTexto))
                    {
                        Logs.Add("⚠️ Fila de categorías incompleta. Se omite el registro.");
                        continue;
                    }

                    var categoria = new CategoriaSocio
                    {
                        EntidadCod = entidad,
                        CatCodigo = codigoCategoria,
                        CatNombre = nombreCategoria,
                        CatDescripcion = descripcionCategoria,
                        EsPredeterminada = string.Equals(esPredeterminadaTexto, "S", StringComparison.OrdinalIgnoreCase),
                        MontoCS = decimal.Parse(montoCsTexto, NumberStyles.Any, CultureInfo.InvariantCulture),
                        ConceptoDescuentoId = int.Parse(conceptoDescuentoTexto)
                    };

                    resultado.Add(categoria);
                }
                catch (Exception ex)
                {
                    Logs.Add($"⚠️ Error mapeando fila de categorías: {ex.Message}");
                }
            }

            return resultado;
        }

        private List<ImportarConsumosDet> MapearConsumosDetalleImportacion()
        {
            var resultado = new List<ImportarConsumosDet>();

            foreach (var fila in _datosConsumosDetalleValidados)
            {
                try
                {
                    fila.TryGetValue("Entidad", out var entidad);
                    fila.TryGetValue("Código Consumo", out var codigoConsumoTexto);
                    fila.TryGetValue("Nro Cuota", out var nroCuotaTexto);
                    fila.TryGetValue("Fecha Vencimiento", out var fechaVencimientoTexto);
                    fila.TryGetValue("Monto", out var montoTexto);

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(codigoConsumoTexto) ||
                        string.IsNullOrWhiteSpace(nroCuotaTexto) ||
                        string.IsNullOrWhiteSpace(fechaVencimientoTexto) ||
                        string.IsNullOrWhiteSpace(montoTexto))
                    {
                        Logs.Add("⚠️ Fila de consumos detalle incompleta. Se omite el registro.");
                        continue;
                    }

                    var registro = new ImportarConsumosDet
                    {
                        Entidad = entidad,
                        CodigoConsumo = int.Parse(codigoConsumoTexto),
                        NroCuota = int.Parse(nroCuotaTexto),
                        FechaVencimiento = DateTime.Parse(fechaVencimientoTexto),
                        Monto = decimal.Parse(montoTexto, NumberStyles.Any, CultureInfo.InvariantCulture)
                    };

                    resultado.Add(registro);
                }
                catch (Exception ex)
                {
                    Logs.Add($"⚠️ Error mapeando fila de consumos detalle: {ex.Message}");
                }
            }

            return resultado;
        }

        private List<CatalogoServicio> MapearCatalogoServicios()
        {
            var resultado = new List<CatalogoServicio>();

            foreach (var fila in _datosCatalogoServiciosValidados)
            {
                try
                {
                    fila.TryGetValue("Entidad", out var entidad);
                    fila.TryGetValue("Servicio", out var servicioNombre);
                    fila.TryGetValue("Importe", out var importeTexto);
                    fila.TryGetValue("Comentarios / Info de Servicio", out var descripcion);

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(servicioNombre) ||
                        string.IsNullOrWhiteSpace(importeTexto))
                    {
                        Logs.Add("⚠️ Fila de catálogo de servicios incompleta. Se omite el registro.");
                        continue;
                    }

                    var registro = new CatalogoServicio
                    {
                        EntidadCod = entidad,
                        ServicioNombre = servicioNombre,
                        Importe = decimal.Parse(importeTexto, NumberStyles.Any, CultureInfo.InvariantCulture),
                        ServicioDescripcion = descripcion
                    };

                    resultado.Add(registro);
                }
                catch (Exception ex)
                {
                    Logs.Add($"⚠️ Error mapeando fila de catálogo de servicios: {ex.Message}");
                }
            }

            return resultado;
        }

        private List<ImportarPadronSocio> MapearPadronSocios()
        {
            var resultado = new List<ImportarPadronSocio>();

            foreach (var fila in _datosValidados)
            {
                try
                {
                    fila.TryGetValue("Entidad", out var entidad);
                    fila.TryGetValue("Nro Socio", out var nroSocioTexto);
                    fila.TryGetValue("Fecha Alta Socio", out var fechaAltaTexto);
                    fila.TryGetValue("Documento", out var documentoTexto);
                    fila.TryGetValue("CUIT", out var cuitTexto);
                    fila.TryGetValue("Código Categoría", out var codigoCategoria);

                    // ❗ Campos obligatorios según la tabla
                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(nroSocioTexto) ||
                        string.IsNullOrWhiteSpace(fechaAltaTexto) ||
                        string.IsNullOrWhiteSpace(documentoTexto) ||
                        string.IsNullOrWhiteSpace(codigoCategoria))
                    {
                        Logs.Add("⚠️ Fila de padrón incompleta. Se omite el registro.");
                        continue;
                    }

                    // ❗ Validaciones de formato
                    if (!TryParseIntFlexible(nroSocioTexto, out var nroSocio) ||
                        !TryParseDateFlexible(fechaAltaTexto, out var fechaAltaSocio) ||
                        !TryParseIntFlexible(documentoTexto, out var documento))
                    {
                        Logs.Add("⚠️ Fila de padrón con formato inválido. Se omite el registro.");
                        continue;
                    }

                    // ✔ CUIT ahora es opcional
                    long? cuit = null;
                    if (!string.IsNullOrWhiteSpace(cuitTexto))
                    {
                        if (!TryParseLongDigitsOnly(cuitTexto, out var cuitParseado))
                        {
                            Logs.Add("⚠️ CUIT inválido. Se omite el registro.");
                            continue;
                        }

                        cuit = cuitParseado;
                    }

                    var registro = new ImportarPadronSocio
                    {
                        Entidad = entidad.Trim(),
                        NroSocio = nroSocio,
                        Documento = documento,
                        Cuit = cuit,              // nullable
                        NroPuesto = null,         // no viene en el archivo
                        CodigoCategoria = codigoCategoria.Trim(),
                        FechaAltaSocio = fechaAltaSocio
                    };

                    resultado.Add(registro);
                }
                catch (Exception ex)
                {
                    Logs.Add($"⚠️ Error mapeando fila de padrón: {ex.Message}");
                }
            }

            return resultado;
        }

        private static bool TryParseIntFlexible(string input, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var sanitized = input.Trim();
            var digits = new string(sanitized.Where(char.IsDigit).ToArray());

            if (string.IsNullOrWhiteSpace(digits))
                return false;

            return int.TryParse(digits, out value);
        }

        private static bool TryParseLongDigitsOnly(string input, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
                return false;

            return long.TryParse(digits, out value);
        }

        private static bool TryParseDateFlexible(string input, out DateTime value)
        {
            return DateTime.TryParse(
                input,
                CultureInfo.GetCultureInfo("es-AR"),
                DateTimeStyles.None,
                out value)
                || DateTime.TryParse(
                    input,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out value);
        }

        private List<ConsumoServicio> MapearConsumosServicios()
        {
            var resultado = new List<ConsumoServicio>();

            foreach (var fila in _datosServiciosValidados)
            {
                try
                {
                    fila.TryGetValue("Entidad", out var entidad);
                    fila.TryGetValue("Nro de Socio", out var nroSocioTexto);
                    fila.TryGetValue("CUIT", out var cuitTexto);
                    fila.TryGetValue("Nro Beneficio", out var nroBeneficioTexto);
                    fila.TryGetValue("Código Consumo", out var codigoConsumoTexto);
                    fila.TryGetValue("Importe Cuota", out var importeCuotaTexto);
                    fila.TryGetValue("Concepto Descuento", out var conceptoDescuentoTexto);

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(nroSocioTexto) ||
                        string.IsNullOrWhiteSpace(cuitTexto) ||
                        string.IsNullOrWhiteSpace(nroBeneficioTexto) ||
                        string.IsNullOrWhiteSpace(codigoConsumoTexto) ||
                        string.IsNullOrWhiteSpace(importeCuotaTexto) ||
                        string.IsNullOrWhiteSpace(conceptoDescuentoTexto))
                    {
                        Logs.Add("⚠️ Fila de servicios incompleta. Se omite el registro.");
                        continue;
                    }

                    var registro = new ConsumoServicio
                    {
                        EntidadCod = entidad,
                        NroSocio = int.Parse(nroSocioTexto),
                        Cuit = long.Parse(cuitTexto),
                        NroBeneficio = int.Parse(nroBeneficioTexto),
                        CodigoConsumo = int.Parse(codigoConsumoTexto),
                        ImporteCuota = decimal.Parse(importeCuotaTexto, NumberStyles.Any, CultureInfo.InvariantCulture),
                        ConceptoDescuentoId = int.Parse(conceptoDescuentoTexto)
                    };

                    resultado.Add(registro);
                }
                catch (Exception ex)
                {
                    Logs.Add($"⚠️ Error mapeando fila de servicios: {ex.Message}");
                }
            }

            return resultado;
        }

        //private List<ImportarConsumoCab> MapearConsumosImportados()
        //{
        //    var resultado = new List<ImportarConsumoCab>();

        //    foreach (var fila in _datosConsumosValidados)
        //    {
        //        try
        //        {
        //            fila.TryGetValue("Entidad", out var entidad);
        //            fila.TryGetValue("Nro Socio", out var nroSocioTexto);
        //            fila.TryGetValue("CUIT", out var cuitTexto);
        //            fila.TryGetValue("Beneficio", out var beneficioTexto);
        //            fila.TryGetValue("Código", out var codigoTexto);
        //            fila.TryGetValue("Cuotas Pendientes", out var cuotasPendientesTexto);
        //            fila.TryGetValue("Monto Deuda", out var montoDeudaTexto);
        //            fila.TryGetValue("Concepto Descuento", out var conceptoDescuentoTexto);

        //            if (string.IsNullOrWhiteSpace(entidad) ||
        //                string.IsNullOrWhiteSpace(nroSocioTexto) ||
        //                string.IsNullOrWhiteSpace(cuitTexto) ||
        //                string.IsNullOrWhiteSpace(beneficioTexto) ||
        //                string.IsNullOrWhiteSpace(codigoTexto) ||
        //                string.IsNullOrWhiteSpace(cuotasPendientesTexto) ||
        //                string.IsNullOrWhiteSpace(montoDeudaTexto) ||
        //                string.IsNullOrWhiteSpace(conceptoDescuentoTexto))
        //            {
        //                Logs.Add("⚠️ Fila de consumos incompleta. Se omite el registro.");
        //                continue;
        //            }

        //            var registro = new ImportarConsumoCab
        //            {
        //                EntidadCod = entidad,
        //                NroSocio = int.Parse(nroSocioTexto),
        //                Cuit = long.Parse(cuitTexto),
        //                Beneficio = int.Parse(beneficioTexto),
        //                CodigoConsumo = long.Parse(codigoTexto),
        //                CuotasPendientes = int.Parse(cuotasPendientesTexto),
        //                MontoDeuda = decimal.Parse(montoDeudaTexto, NumberStyles.Any, CultureInfo.InvariantCulture),
        //                ConceptoDescuentoId = int.Parse(conceptoDescuentoTexto)
        //            };

        //            resultado.Add(registro);
        //        }
        //        catch (Exception ex)
        //        {
        //            Logs.Add($"⚠️ Error mapeando fila de consumos: {ex.Message}");
        //        }
        //    }

        //    return resultado;
        //}

        private List<ImportarConsumoCab> MapearConsumosImportados()
        {
            var resultado = new List<ImportarConsumoCab>();

            foreach (var fila in _datosConsumosValidados)
            {
                try
                {
                    fila.TryGetValue("Entidad", out var entidad);
                    fila.TryGetValue("Nro Socio", out var nroSocioTexto);
                    fila.TryGetValue("CUIT", out var cuitTexto);
                    fila.TryGetValue("Código", out var codigoTexto);
                    fila.TryGetValue("Cuotas Pendientes", out var cuotasPendientesTexto);
                    fila.TryGetValue("Monto Deuda", out var montoDeudaTexto);
                    fila.TryGetValue("Concepto Descuento", out var conceptoDescuentoTexto);

                    // 🔴 Beneficio eliminado (ya no existe en la tabla)

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(nroSocioTexto) ||
                        string.IsNullOrWhiteSpace(codigoTexto) ||
                        string.IsNullOrWhiteSpace(cuotasPendientesTexto) ||
                        string.IsNullOrWhiteSpace(montoDeudaTexto) ||
                        string.IsNullOrWhiteSpace(conceptoDescuentoTexto))
                    {
                        Logs.Add("⚠️ Fila de consumos incompleta. Se omite el registro.");
                        continue;
                    }

                    var registro = new ImportarConsumoCab
                    {
                        Entidad = entidad,
                        NroSocio = int.Parse(nroSocioTexto),

                        // Ahora permite NULL
                        Cuit = string.IsNullOrWhiteSpace(cuitTexto)
                            ? null
                            : long.Parse(cuitTexto),

                        // Ahora permite NULL en BD
                        NroPuesto = null,

                        CodigoConsumo = long.Parse(codigoTexto),
                        CuotasPendientes = int.Parse(cuotasPendientesTexto),

                        MontoDeuda = decimal.Parse(
                            montoDeudaTexto,
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture),

                        ConceptoDescuento = int.Parse(conceptoDescuentoTexto)
                    };

                    resultado.Add(registro);
                }
                catch (Exception ex)
                {
                    Logs.Add($"⚠️ Error mapeando fila de consumos: {ex.Message}");
                }
            }

            return resultado;
        }

        private bool PuedeCopiarABase(object? parameter)
        {
            return ValidacionFinalizada;
        }

        private void CopiarABase(object? parameter)
        {
            Logs.Add("💾 Iniciando proceso de copia a base de datos...");
        }

        private void LimpiarPantalla()
        {
            // Limpiar selecciones
            EmpleadorSeleccionado = null;
            EntidadSeleccionada = null;

            // Limpiar rutas de archivos
            ArchivoCategorias = null;
            ArchivoPadron = null;
            ArchivoConsumos = null;
            ArchivoConsumosDetalle = null;
            ArchivoServicios = null;
            ArchivoCatalogoServicios = null;

            // Limpiar logs y estado
            Logs.Clear();
            Progreso = 0;
            EstaProcesando = false;
            ValidacionFinalizada = false;
        }

        private void ExportarLog()
        {
            if (Logs == null || Logs.Count == 0)
            {
                Logs?.Add("⚠️ No hay mensajes de log para exportar.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Guardar log de validación",
                Filter = "Archivo de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                FileName = "LogMigracion.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllLines(dialog.FileName, Logs);
                    Logs.Add($"✅ Log exportado a: {dialog.FileName}");
                }
                catch (Exception ex)
                {
                    Logs.Add($"❌ Error al exportar el log: {ex.Message}");
                }
            }
        }

        private async Task CopiarABaseAsync()
        {
            EstaProcesando = true;
            Progreso = 0;

            try
            {
                using (var db = new AppDbContext())
                {
                    if (!string.IsNullOrWhiteSpace(ArchivoPadron))
                    {
                        var padronSocios = MapearPadronSocios();
                        if (padronSocios.Any())
                        {
                            db.InsertPadronSocio(padronSocios);
                            Progreso = 20;
                            Logs.Add($"Padron de socios insertado correctamente en Padron_socios ({padronSocios.Count} registros).");
                        }
                        else
                        {
                            Logs.Add("No hay registros validos de padron para insertar en Padron_socios.");
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(ArchivoCategorias))
                    {
                        var categorias = MapearCategorias();
                        if (categorias.Any())
                        {
                            Logs.Add($"Insertando {categorias.Count} categorias en Categorias_Socio...");
                            db.InsertCategoriasSocio(categorias);
                            Progreso = 40;
                            Logs.Add("Categorias insertadas correctamente en Categorias_Socio.");
                        }
                        else
                        {
                            Logs.Add("No hay categorias validas para insertar en Categorias_Socio.");
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(ArchivoConsumosDetalle))
                    {
                        var consumosDetalleImport = MapearConsumosDetalleImportacion();
                        if (consumosDetalleImport.Any())
                        {
                            Logs.Add($"Insertando {consumosDetalleImport.Count} registros en Importar_Consumos_Detalle...");
                            db.InsertImportarConsumosDet(consumosDetalleImport);
                            Progreso = 60;
                            Logs.Add("Consumos detalle insertados correctamente en Importar_Consumos_Detalle.");
                        }
                        else
                        {
                            Logs.Add("No hay consumos detalle validos para insertar en Importar_Consumos_Detalle.");
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(ArchivoCatalogoServicios))
                    {
                        var catalogoServicios = MapearCatalogoServicios();
                        if (catalogoServicios.Any())
                        {
                            Logs.Add($"Insertando {catalogoServicios.Count} registros en Catalogo_Servicios...");
                            db.InsertCatalogoServicios(catalogoServicios);
                            Progreso = 75;
                            Logs.Add("Catalogo de servicios insertado correctamente en Catalogo_Servicios.");
                        }
                        else
                        {
                            Logs.Add("No hay registros validos para insertar en Catalogo_Servicios.");
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(ArchivoServicios))
                    {
                        var consumosServicios = MapearConsumosServicios();
                        if (consumosServicios.Any())
                        {
                            Logs.Add($"Insertando {consumosServicios.Count} registros en Consumos_Servicios...");
                            db.InsertConsumosServicios(consumosServicios);
                            Progreso = 90;
                            Logs.Add("Servicios insertados correctamente en Consumos_Servicios.");
                        }
                        else
                        {
                            Logs.Add("No hay registros validos para insertar en Consumos_Servicios.");
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(ArchivoConsumos))
                    {
                        var consumosImportados = MapearConsumosImportados();
                        if (consumosImportados.Any())
                        {
                            Logs.Add($"Insertando {consumosImportados.Count} registros en Consumo...");
                            db.InsertImportarConsumoCab(consumosImportados);
                            Progreso = 100;
                            Logs.Add("Consumos insertados correctamente en tabla Consumo.");
                        }
                        else
                        {
                            Logs.Add("No hay registros validos para insertar en tabla Consumo.");
                        }
                    }
                }
            }
            finally
            {
                EstaProcesando = false;
            }

            await Task.CompletedTask;
        }
    }
}

