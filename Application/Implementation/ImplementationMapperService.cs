using ImplementadorCUAD.Models;
using System.Globalization;
using ImplementadorCUAD.Infrastructure;

namespace ImplementadorCUAD.Services
{
    public class ImplementationMapperService
    {
        public List<ImportarPadronSocio> MapPadronSocios(IEnumerable<Dictionary<string, string>> data, IAppLogger log)
        {
            var resultado = new List<ImportarPadronSocio>();

            foreach (var row in data)
            {
                try
                {
                    row.TryGetValue("Entidad", out var entidad);
                    row.TryGetValue("Nro Socio", out var nroSocioTexto);
                    row.TryGetValue("Fecha Alta Socio", out var fechaAltaTexto);
                    row.TryGetValue("Documento", out var documentoTexto);
                    row.TryGetValue("CUIT", out var cuitTexto);
                    row.TryGetValue("Código Categoría", out var codigoCategoria);

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(nroSocioTexto) ||
                        string.IsNullOrWhiteSpace(fechaAltaTexto) ||
                        string.IsNullOrWhiteSpace(documentoTexto) ||
                        string.IsNullOrWhiteSpace(codigoCategoria))
                    {
                        log.Warn("Fila de padrón socio incompleta. Se omite el registro.");
                        continue;
                    }

                    if (!TryParseIntFlexible(nroSocioTexto, out var nroSocio) ||
                        !TryParseDateFlexible(fechaAltaTexto, out var fechaAltaSocio) ||
                        !TryParseIntFlexible(documentoTexto, out var documento))
                    {
                        log.Warn("Fila de padrón socio con formato inválido. Se omite el registro.");
                        continue;
                    }

                    long? cuit = null;
                    if (!string.IsNullOrWhiteSpace(cuitTexto))
                    {
                        if (!TryParseLongDigitsOnly(cuitTexto, out var cuitParseado))
                        {
                            log.Warn("CUIT inválido. Se omite el registro.");
                            continue;
                        }

                        cuit = cuitParseado;
                    }

                    var registro = new ImportarPadronSocio
                    {
                        Entidad = entidad.Trim(),
                        NroSocio = nroSocio,
                        Documento = documento,
                        Cuit = cuit,
                        NroPuesto = null,
                        CodigoCategoria = codigoCategoria.Trim(),
                        FechaAltaSocio = fechaAltaSocio
                    };

                    resultado.Add(registro);
                }
                catch (Exception ex)
                {
                    log.Error($"Error mapeando row de padrón: {ex.Message}");
                }
            }

            return resultado;
        }

        public List<ImportarConsumoCab> MapConsumos(IEnumerable<Dictionary<string, string>> data, IAppLogger log)
        {
            var resultado = new List<ImportarConsumoCab>();

            foreach (var row in data)
            {
                try
                {
                    row.TryGetValue("Entidad", out var entidad);
                    row.TryGetValue("Nro Socio", out var nroSocioTexto);
                    row.TryGetValue("CUIT", out var cuitTexto);
                    if (!row.TryGetValue("Codigo Consumo", out var codigoTexto) &&
                        !row.TryGetValue("Código Consumo", out codigoTexto) &&
                        !row.TryGetValue("Código", out codigoTexto))
                    {
                        codigoTexto = null;
                    }
                    row.TryGetValue("Cuotas Pendientes", out var cuotasPendientesTexto);
                    row.TryGetValue("Monto Deuda", out var montoDeudaTexto);
                    row.TryGetValue("Concepto Descuento", out var conceptoDescuentoTexto);

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(nroSocioTexto) ||
                        string.IsNullOrWhiteSpace(codigoTexto) ||
                        string.IsNullOrWhiteSpace(cuotasPendientesTexto) ||
                        string.IsNullOrWhiteSpace(montoDeudaTexto) ||
                        string.IsNullOrWhiteSpace(conceptoDescuentoTexto))
                    {
                        log.Warn("Fila de consumos incompleta. Se omite el registro.");
                        continue;
                    }

                    var registro = new ImportarConsumoCab
                    {
                        Entidad = entidad,
                        NroSocio = int.Parse(nroSocioTexto),
                        Cuit = string.IsNullOrWhiteSpace(cuitTexto) ? null : long.Parse(cuitTexto),
                        CodigoConsumo = long.Parse(codigoTexto),
                        CuotasPendientes = int.Parse(cuotasPendientesTexto),
                        MontoDeuda = decimal.Parse(montoDeudaTexto, NumberStyles.Any, CultureInfo.InvariantCulture),
                        ConceptoDescuento = int.Parse(conceptoDescuentoTexto)
                    };

                    resultado.Add(registro);
                }
                catch (Exception ex)
                {
                    log.Error($"Error mapeando row de consumos: {ex.Message}");
                }
            }

            return resultado;
        }

        public List<ImportarConsumosDet> MapConsumosDetalle(IEnumerable<Dictionary<string, string>> data, IAppLogger log)
        {
            var resultado = new List<ImportarConsumosDet>();

            foreach (var row in data)
            {
                try
                {
                    row.TryGetValue("Entidad", out var entidad);
                    row.TryGetValue("Código Consumo", out var codigoConsumoTexto);
                    row.TryGetValue("Nro Cuota", out var nroCuotaTexto);
                    row.TryGetValue("Fecha Vencimiento", out var fechaVencimientoTexto);
                    row.TryGetValue("Monto", out var montoTexto);

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(codigoConsumoTexto) ||
                        string.IsNullOrWhiteSpace(nroCuotaTexto) ||
                        string.IsNullOrWhiteSpace(fechaVencimientoTexto) ||
                        string.IsNullOrWhiteSpace(montoTexto))
                    {
                        log.Warn("Fila de consumos detalle incompleta. Se omite el registro.");
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
                    log.Error($"Error mapeando row de consumos detalle: {ex.Message}");
                }
            }

            return resultado;
        }

        private static bool TryParseIntFlexible(string input, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var sanitized = input.Trim();
            var digits = new string(sanitized.Where(char.IsDigit).ToArray());

            if (string.IsNullOrWhiteSpace(digits))
            {
                return false;
            }

            return int.TryParse(digits, out value);
        }

        private static bool TryParseLongDigitsOnly(string input, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
            {
                return false;
            }

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
    }
}
