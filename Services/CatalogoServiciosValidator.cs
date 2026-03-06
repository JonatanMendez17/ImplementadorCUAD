using ImplementadorCUAD.Models;
using ImplementadorCUAD.Infrastructure;
using System.Globalization;

namespace ImplementadorCUAD.Services;

public sealed class CatalogoServiciosValidator(IAppDbContextFactory dbContextFactory)
{
    private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

    public void Apply(ImplementacionValidationResult result, Action<string> log)
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
            log($"Catalogo Servicios: no se pudo leer el catalogo de servicios de CUAD. {ex.Message}");
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
            var fila = result.DatosCatalogoServiciosValidados[i];
            var numeroFila = i + 2;
            var filaValida = true;

            var entidad = GetFirstValue(fila, "Entidad");
            var servicio = GetFirstValue(fila, "Servicio");
            var importeTexto = GetFirstValue(fila, "Importe");

            if (string.IsNullOrWhiteSpace(entidad) || string.IsNullOrWhiteSpace(servicio))
            {
                log($"Catalogo Servicios fila {numeroFila}: La entidad se encuentra vacia.");
                filaValida = false;
            }
            else
            {
                var clave = $"{entidad.Trim()}|{servicio.Trim()}";
                if (!catalogoPorEntidadServicio.TryGetValue(clave, out var refCuad))
                {
                    log($" Catalogo Servicios fila {numeroFila}: servicio '{servicio}' no existe en CUAD para la entidad '{entidad}'.");
                    filaValida = false;
                }
                else
                {
                    if (!TryParseDecimalFlexible(importeTexto, out var importeArchivo))
                    {
                        log($"Catalogo Servicios fila {numeroFila}: El importe '{importeTexto}' es invalido.");
                        filaValida = false;
                    }
                    else
                    {
                        var diferencia = Math.Abs(importeArchivo - refCuad.Importe);
                        if (diferencia > 0.01m)
                        {
                            log($"Catalogo Servicios fila {numeroFila}: El importe '{importeArchivo}' no coincide con CUAD ({refCuad.Importe}).");
                            filaValida = false;
                        }
                    }
                }
            }

            if (filaValida)
            {
                filtrado.Add(fila);
            }
            else
            {
                rechazadas++;
            }
        }

        if (rechazadas > 0)
        {
            log($"Resumen validaciones Catalogo Servicios: aceptadas={filtrado.Count}, rechazadas={rechazadas}.");
        }

        result.DatosCatalogoServiciosValidados = filtrado;
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
}

