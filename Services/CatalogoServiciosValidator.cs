using ImplementadorCUAD.Models;
using ImplementadorCUAD.Infrastructure;
using System.Globalization;

namespace ImplementadorCUAD.Services;

public sealed class CatalogoServiciosValidator(IAppDbContextFactory dbContextFactory)
{
    private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

    public void Apply(ImplementationValidationResult result, IAppLogger log)
    {
        if (result.DatosCatalogoServiciosValidados.Count == 0)
        {
            return;
        }

        List<CatalogoServicioCuadRef> catalogoCuad;
        try
        {
            using var db = _dbContextFactory.Create();
            catalogoCuad = db.GetCatalogoServiciosCuad();
        }
        catch (Exception ex)
        {
            log.Error($"Catalogo Servicios: no se pudo leer el catálogo de servicios de la base. {ex.Message}");
            result.DatosCatalogoServiciosValidados = [];
            return;
        }

        var catalogoPorEntidadServicio = catalogoCuad
            .ToDictionary(
                c => $"{c.Entidad.Trim()}|{c.Servicio.Trim()}",
                c => c,
                StringComparer.OrdinalIgnoreCase);

        var filtrado = new List<Dictionary<string, string>>();
        var rechazadas = 0;

        for (int i = 0; i < result.DatosCatalogoServiciosValidados.Count; i++)
        {
            var row = result.DatosCatalogoServiciosValidados[i];
            var rowNumber = i + 2;
            var filaValida = true;

            var entidad = GetFirstValue(row, "Entidad");
            var servicio = GetFirstValue(row, "Servicio");
            var importeTexto = GetFirstValue(row, "Importe");

            if (string.IsNullOrWhiteSpace(entidad) || string.IsNullOrWhiteSpace(servicio))
            {
                log.Warn($"Catalogo Servicios row {rowNumber}: La entidad se encuentra vacia.");
                filaValida = false;
            }
            else
            {
                var clave = $"{entidad.Trim()}|{servicio.Trim()}";
                if (!catalogoPorEntidadServicio.TryGetValue(clave, out var refCuad))
                {
                    log.Warn($" Catalogo Servicios row {rowNumber}: servicio '{servicio}' no existe en la base para la entidad '{entidad}'.");
                    filaValida = false;
                }
                else
                {
                    if (!TryParseDecimalFlexible(importeTexto, out var importeArchivo))
                    {
                        log.Warn($"Catalogo Servicios row {rowNumber}: El importe '{importeTexto}' es invalido.");
                        filaValida = false;
                    }
                    else
                    {
                        var diferencia = Math.Abs(importeArchivo - refCuad.Importe);
                        if (diferencia > 0.01m)
                        {
                            log.Warn($"Catalogo Servicios row {rowNumber}: El importe '{importeArchivo}' no coincide con la base ({refCuad.Importe}).");
                            filaValida = false;
                        }
                    }
                }
            }

            if (filaValida)
            {
                filtrado.Add(row);
            }
            else
            {
                rechazadas++;
            }
        }

        if (rechazadas > 0)
        {
            log.Info($"Resumen validaciones Catalogo Servicios: aceptadas={filtrado.Count}, rechazadas={rechazadas}.");
        }

        result.DatosCatalogoServiciosValidados = filtrado;
    }

    private static string GetFirstValue(Dictionary<string, string> row, params string[] posiblesClaves)
    {
        return TryGetFirstValue(row, out var value, posiblesClaves) ? value : string.Empty;
    }

    private static bool TryGetFirstValue(Dictionary<string, string> row, out string value, params string[] posiblesClaves)
    {
        foreach (var clave in posiblesClaves)
        {
            if (row.TryGetValue(clave, out var encontrado))
            {
                value = encontrado;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryParseDecimalFlexible(string texto, out decimal value)
    {
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ||
               decimal.TryParse(texto, NumberStyles.Number, CultureInfo.GetCultureInfo("es-AR"), out value);
    }
}

