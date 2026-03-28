using Implementador.Models;
using Implementador.Infrastructure;
using System.Globalization;
using Implementador.Application.Validation.Common;
using Implementador.Application.Validation.Core;

namespace Implementador.Application.Validation;

public sealed class ConsumosDetalleValidator : RowValidatorBase
{
    public void Apply(ImplementationValidationResult result, IAppLogger log, ValidationReferenceData? snapshot = null)
    {
        if (result.DatosConsumosDetalleValidados.Count == 0)
        {
            return;
        }

        log.Separator();
        var safeSnapshot = snapshot ?? ValidationReferenceData.Empty;
        var entidadesRef = safeSnapshot.EntidadesRef;

        var consumosPorCodigo = result.DatosConsumosValidados
            .Where(f => !string.IsNullOrWhiteSpace(RowValueReader.GetFirstValue(f, "Codigo Consumo", "Código Consumo")))
            .GroupBy(f => RowValueReader.GetFirstValue(f, "Codigo Consumo", "Código Consumo").Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var consumosDisponible = consumosPorCodigo.Count > 0;
        if (!consumosDisponible)
        {
            log.Warn("Consumos Detalle: no se cargó archivo de Consumos. No se puede verificar que el Codigo Consumo exista en el archivo de Consumos.");
        }

        var detalleFiltrado = FilterValidRows(
            ArchivoNombre.ConsumosDetalle,
            result.DatosConsumosDetalleValidados,
            log,
            (row, rowNumber) =>
            {
            var erroresFila = new List<string>();

            var entidad = RowValueReader.GetFirstValue(row, "Entidad");
            var codigoConsumo = RowValueReader.GetFirstValue(row, "Codigo Consumo", "Código Consumo");
            var fechaVencimientoText = RowValueReader.GetFirstValue(row, "Fecha Vencimiento");

            if (!entidadesRef.Contains(entidad!.Trim()))
            {
                erroresFila.Add($"El campo (Entidad) '{entidad}' no existe en la base.");
            }

            if (consumosDisponible && !consumosPorCodigo.ContainsKey(codigoConsumo!.Trim()))
            {
                erroresFila.Add($"El campo (Codigo Consumo) '{codigoConsumo}' no existe en archivo de Consumos.");
            }

            if (!ValueParsers.TryParseDateFlexible(fechaVencimientoText, out var fechaVencimiento))
            {
                erroresFila.Add("El campo (Fecha Vencimiento) no es una fecha valida.");
            }
            else if (fechaVencimiento.Date <= DateTime.Today)
            {
                erroresFila.Add("El campo (Fecha Vencimiento) no puede ser hoy o anterior.");
            }

            return erroresFila;
            },
            out var rechazadas);

        var detallePorCodigo = detalleFiltrado
            .Where(f => !string.IsNullOrWhiteSpace(RowValueReader.GetFirstValue(f, "Codigo Consumo", "Código Consumo")))
            .GroupBy(f => RowValueReader.GetFirstValue(f, "Codigo Consumo", "Código Consumo").Trim(), StringComparer.OrdinalIgnoreCase)
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

            var cuotasPendientesText = RowValueReader.GetFirstValue(consumoFila, "Cuotas Pendientes");
            var montoDeudaText = RowValueReader.GetFirstValue(consumoFila, "Monto Deuda");

            if (!int.TryParse(cuotasPendientesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cuotasEsperadas))
            {
                codigosInvalidosPorTotales.Add(codigo);
                log.Warn($"Consumos Detalle: No se pudo leer 'Cuotas Pendientes' para el codigo de consumo '{codigo}'.");
                continue;
            }

            if (!ValueParsers.TryParseDecimalFlexible(montoDeudaText, out var montoEsperado))
            {
                codigosInvalidosPorTotales.Add(codigo);
                log.Warn($"Consumos Detalle: No se pudo leer el 'Monto Deuda' para el codigo de consumo '{codigo}'.");
                continue;
            }

            var cuotasDetalle = filasDetalle.Count;
            var sumaDetalle = 0m;
            var parseOk = true;
            var numerosCuota = new List<int>(cuotasDetalle);
            foreach (var filaDetalle in filasDetalle)
            {
                var montoText = RowValueReader.GetFirstValue(filaDetalle, "Monto");
                if (!ValueParsers.TryParseDecimalFlexible(montoText, out var montoCuota))
                {
                    parseOk = false;
                    break;
                }

                var nroCuotaText = RowValueReader.GetFirstValue(filaDetalle, "Nro Cuota");
                if (!int.TryParse(nroCuotaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nroCuota))
                {
                    parseOk = false;
                    break;
                }

                numerosCuota.Add(nroCuota);
                sumaDetalle += montoCuota;
            }

            if (!parseOk)
            {
                codigosInvalidosPorTotales.Add(codigo);
                log.Warn($"Consumos Detalle: Para el código de consumo '{codigo}' hay al menos una fila con 'Monto' o 'Nro Cuota' inválidos.");
                continue;
            }

            numerosCuota.Sort();
            var consecutivas = true;
            for (int i = 0; i < numerosCuota.Count; i++)
            {
                var esperado = i + 1;
                if (numerosCuota[i] != esperado)
                {
                    consecutivas = false;
                    break;
                }
            }
            if (!consecutivas)
            {
                codigosInvalidosPorTotales.Add(codigo);
                log.Warn($"Consumos Detalle: Los Nro Cuota no son consecutivos para codigo de consumo '{codigo}'.");
                continue;
            }

            var sumaCoincide = Math.Abs(sumaDetalle - montoEsperado) <= 0.01m;
            if (cuotasDetalle != cuotasEsperadas || !sumaCoincide)
            {
                codigosInvalidosPorTotales.Add(codigo);
                log.Warn($"Consumos Detalle: La cantidad de cuotas y monto deuda no coinciden para el codigo de consumo '{codigo}'. Esperado cuotas={cuotasEsperadas}, monto={montoEsperado}. Detalle cuotas={cuotasDetalle}, monto={sumaDetalle}.");
            }
        }

        if (codigosInvalidosPorTotales.Count > 0)
        {
            var depurado = new List<Dictionary<string, string>>();
            foreach (var row in detalleFiltrado)
            {
                var codigo = RowValueReader.GetFirstValue(row, "Codigo Consumo", "Código Consumo").Trim();
                if (codigosInvalidosPorTotales.Contains(codigo))
                {
                    rechazadas++;
                    continue;
                }

                depurado.Add(row);
            }

            detalleFiltrado = depurado;
        }

        if (rechazadas > 0)
            log.Info(ValidationLog.ReglaRechazadas(ArchivoNombre.ConsumosDetalle, rechazadas, rechazadas + detalleFiltrado.Count));
        log.Info(ValidationLog.ListasParaImplementar(ArchivoNombre.ConsumosDetalle, detalleFiltrado.Count));

        result.DatosConsumosDetalleValidados = detalleFiltrado;
    }

}



