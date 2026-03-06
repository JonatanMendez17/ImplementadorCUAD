using ImplementadorCUAD.Models;
using System.Globalization;

namespace ImplementadorCUAD.Services
{
    public class ImplementacionMapperService
    {
        public List<ImportarPadronSocio> MapPadronSocios(IEnumerable<Dictionary<string, string>> datos, Action<string> log)
        {
            var resultado = new List<ImportarPadronSocio>();

            foreach (var fila in datos)
            {
                try
                {
                    fila.TryGetValue("Entidad", out var entidad);
                    fila.TryGetValue("Nro Socio", out var nroSocioTexto);
                    fila.TryGetValue("Fecha Alta Socio", out var fechaAltaTexto);
                    fila.TryGetValue("Documento", out var documentoTexto);
                    fila.TryGetValue("CUIT", out var cuitTexto);
                    fila.TryGetValue("Código Categoría", out var codigoCategoria);

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(nroSocioTexto) ||
                        string.IsNullOrWhiteSpace(fechaAltaTexto) ||
                        string.IsNullOrWhiteSpace(documentoTexto) ||
                        string.IsNullOrWhiteSpace(codigoCategoria))
                    {
                        log("Fila de padrón socio incompleta. Se omite el registro.");
                        continue;
                    }

                    if (!TryParseIntFlexible(nroSocioTexto, out var nroSocio) ||
                        !TryParseDateFlexible(fechaAltaTexto, out var fechaAltaSocio) ||
                        !TryParseIntFlexible(documentoTexto, out var documento))
                    {
                        log("Fila de padrón socio con formato inválido. Se omite el registro.");
                        continue;
                    }

                    long? cuit = null;
                    if (!string.IsNullOrWhiteSpace(cuitTexto))
                    {
                        if (!TryParseLongDigitsOnly(cuitTexto, out var cuitParseado))
                        {
                            log("CUIT inválido. Se omite el registro.");
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
                    log($"Error mapeando fila de padrón: {ex.Message}");
                }
            }

            return resultado;
        }

        public List<ImportarConsumoCab> MapConsumos(IEnumerable<Dictionary<string, string>> datos, Action<string> log)
        {
            var resultado = new List<ImportarConsumoCab>();

            foreach (var fila in datos)
            {
                try
                {
                    fila.TryGetValue("Entidad", out var entidad);
                    fila.TryGetValue("Nro Socio", out var nroSocioTexto);
                    fila.TryGetValue("CUIT", out var cuitTexto);
                    if (!fila.TryGetValue("Codigo Consumo", out var codigoTexto) &&
                        !fila.TryGetValue("Código Consumo", out codigoTexto) &&
                        !fila.TryGetValue("Código", out codigoTexto))
                    {
                        codigoTexto = null;
                    }
                    fila.TryGetValue("Cuotas Pendientes", out var cuotasPendientesTexto);
                    fila.TryGetValue("Monto Deuda", out var montoDeudaTexto);
                    fila.TryGetValue("Concepto Descuento", out var conceptoDescuentoTexto);

                    if (string.IsNullOrWhiteSpace(entidad) ||
                        string.IsNullOrWhiteSpace(nroSocioTexto) ||
                        string.IsNullOrWhiteSpace(codigoTexto) ||
                        string.IsNullOrWhiteSpace(cuotasPendientesTexto) ||
                        string.IsNullOrWhiteSpace(montoDeudaTexto) ||
                        string.IsNullOrWhiteSpace(conceptoDescuentoTexto))
                    {
                        log("Fila de consumos incompleta. Se omite el registro.");
                        continue;
                    }

                    var registro = new ImportarConsumoCab
                    {
                        Entidad = entidad,
                        NroSocio = int.Parse(nroSocioTexto),
                        Cuit = string.IsNullOrWhiteSpace(cuitTexto) ? null : long.Parse(cuitTexto),
                        NroPuesto = null,
                        CodigoConsumo = long.Parse(codigoTexto),
                        CuotasPendientes = int.Parse(cuotasPendientesTexto),
                        MontoDeuda = decimal.Parse(montoDeudaTexto, NumberStyles.Any, CultureInfo.InvariantCulture),
                        ConceptoDescuento = int.Parse(conceptoDescuentoTexto)
                    };

                    resultado.Add(registro);
                }
                catch (Exception ex)
                {
                    log($"Error mapeando fila de consumos: {ex.Message}");
                }
            }

            return resultado;
        }

        public List<ImportarConsumosDet> MapConsumosDetalle(IEnumerable<Dictionary<string, string>> datos, Action<string> log)
        {
            var resultado = new List<ImportarConsumosDet>();

            foreach (var fila in datos)
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
                        log("Fila de consumos detalle incompleta. Se omite el registro.");
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
                    log($"Error mapeando fila de consumos detalle: {ex.Message}");
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
