using ImplementadorCUAD.Models;
using ImplementadorCUAD.Infrastructure;
using System.Globalization;

namespace ImplementadorCUAD.Services;

public sealed class ConsumosDetalleValidator(IAppDbContextFactory dbContextFactory)
{
    private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

    public void Apply(ImplementacionValidationResult result, Action<string> log)
    {
        if (result.DatosConsumosDetalleValidados.Count == 0)
        {
            return;
        }

        HashSet<string> entidadesCuad;
        try
        {
            using var db = _dbContextFactory.Create();
            entidadesCuad = db.GetEntidad()
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
            log($"Consumos Detalle: No se pudo validar entidades de CUAD. {ex.Message}");
            result.DatosConsumosDetalleValidados = new List<Dictionary<string, string>>();
            return;
        }

        var consumosPorCodigo = result.DatosConsumosValidados
            .Where(f => !string.IsNullOrWhiteSpace(GetFirstValue(f, "Codigo Consumo", "Código Consumo")))
            .GroupBy(f => GetFirstValue(f, "Codigo Consumo", "Código Consumo").Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var detalleFiltrado = new List<Dictionary<string, string>>();
        var rechazadas = 0;

        for (int i = 0; i < result.DatosConsumosDetalleValidados.Count; i++)
        {
            var fila = result.DatosConsumosDetalleValidados[i];
            var numeroFila = i + 2;
            var erroresFila = new List<string>();

            var entidad = GetFirstValue(fila, "Entidad");
            var codigoConsumo = GetFirstValue(fila, "Codigo Consumo", "Código Consumo");
            var fechaVencimientoText = GetFirstValue(fila, "Fecha Vencimiento");

            if (string.IsNullOrWhiteSpace(entidad) || !entidadesCuad.Contains(entidad.Trim()))
            {
                erroresFila.Add($"La entidad '{entidad}' no existe en CUAD.");
            }

            if (string.IsNullOrWhiteSpace(codigoConsumo) || !consumosPorCodigo.ContainsKey(codigoConsumo.Trim()))
            {
                erroresFila.Add($"El codigo de consumo '{codigoConsumo}' no existe en archivo de Consumos detalle.");
            }

            if (!TryParseDateFlexible(fechaVencimientoText, out var fechaVencimiento))
            {
                erroresFila.Add("La fecha de vencimiento es invalida.");
            }
            else if (fechaVencimiento.Date <= DateTime.Today)
            {
                erroresFila.Add("La fecha de vencimiento no puede ser hoy o anterior.");
            }

            if (erroresFila.Count == 0)
            {
                detalleFiltrado.Add(fila);
            }
            else
            {
                rechazadas++;
                log($"Consumos Detalle fila {numeroFila}: {string.Join(" | ", erroresFila)}");
            }
        }

        var detallePorCodigo = detalleFiltrado
            .Where(f => !string.IsNullOrWhiteSpace(GetFirstValue(f, "Codigo Consumo", "Código Consumo")))
            .GroupBy(f => GetFirstValue(f, "Codigo Consumo", "Código Consumo").Trim(), StringComparer.OrdinalIgnoreCase)
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
                log($"Consumos Detalle: No se pudo leer 'Cuotas Pendientes' para el codigo de consumo '{codigo}'.");
                continue;
            }

            if (!TryParseDecimalFlexible(montoDeudaText, out var montoEsperado))
            {
                codigosInvalidosPorTotales.Add(codigo);
                log($"Consumos Detalle: No se pudo leer el 'Monto Deuda' para el codigo de consumo '{codigo}'.");
                continue;
            }

            var cuotasDetalle = filasDetalle.Count;
            var sumaDetalle = 0m;
            var parseOk = true;
            var numerosCuota = new List<int>(cuotasDetalle);
            foreach (var filaDetalle in filasDetalle)
            {
                var montoText = GetFirstValue(filaDetalle, "Monto");
                if (!TryParseDecimalFlexible(montoText, out var montoCuota))
                {
                    parseOk = false;
                    break;
                }

                var nroCuotaText = GetFirstValue(filaDetalle, "Nro Cuota");
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
                log($"Consumos Detalle: Para el código de consumo '{codigo}' hay al menos una fila con 'Monto' o 'Nro Cuota' inválidos.");
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
                log($"Consumos Detalle: Los Nro Cuota no son consecutivos para codigo de consumo '{codigo}'.");
                continue;
            }

            var sumaCoincide = Math.Abs(sumaDetalle - montoEsperado) <= 0.01m;
            if (cuotasDetalle != cuotasEsperadas || !sumaCoincide)
            {
                codigosInvalidosPorTotales.Add(codigo);
                log($"Consumos Detalle: La cantidad de cuotas y monto deuda no coinciden para el codigo de consumo '{codigo}'. Esperado cuotas={cuotasEsperadas}, monto={montoEsperado}. Detalle cuotas={cuotasDetalle}, monto={sumaDetalle}.");
            }
        }

        if (codigosInvalidosPorTotales.Count > 0)
        {
            var depurado = new List<Dictionary<string, string>>();
            foreach (var fila in detalleFiltrado)
            {
                var codigo = GetFirstValue(fila, "Codigo Consumo", "Código Consumo").Trim();
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
            log($"Resumen validacion Consumos Detalle: aceptadas={detalleFiltrado.Count}, rechazadas={rechazadas}.");
        }

        result.DatosConsumosDetalleValidados = detalleFiltrado;
    }

    private static string GetFirstValue(Dictionary<string, string> fila, params string[] posiblesClaves)
    {
        return TryGetFirstValue(fila, out var value, posiblesClaves) ? value : string.Empty;
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

}

