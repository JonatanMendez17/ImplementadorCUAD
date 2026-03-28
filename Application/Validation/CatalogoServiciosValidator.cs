using Implementador.Models;
using Implementador.Infrastructure;
using Implementador.Application.Validation.Common;
using Implementador.Application.Validation.Core;

namespace Implementador.Application.Validation;

public sealed class CatalogoServiciosValidator : RowValidatorBase
{
    public void Apply(ImplementationValidationResult result, IAppLogger log, ValidationReferenceData? snapshot = null)
    {
        if (result.DatosCatalogoServiciosValidados.Count == 0)
        {
            return;
        }

        log.Separator();
        var safeSnapshot = snapshot ?? ValidationReferenceData.Empty;
        var catalogoPorEntidadServicio = safeSnapshot.CatalogoPorEntidadServicio;

        var filtrado = FilterValidRows(
            ArchivoNombre.CatalogoServicios,
            result.DatosCatalogoServiciosValidados,
            log,
            (row, rowNumber) =>
            {
                var erroresFila = new List<string>();
                var entidad = RowValueReader.GetFirstValue(row, "Entidad");
                var servicio = RowValueReader.GetFirstValue(row, "Servicio");
                var importeTexto = RowValueReader.GetFirstValue(row, "Importe");

                var clave = $"{entidad!.Trim()}|{servicio!.Trim()}";
                if (!catalogoPorEntidadServicio.TryGetValue(clave, out var refCatalogo))
                {
                    erroresFila.Add($"El campo (Servicio) '{servicio}' no existe en la base para la entidad '{entidad}'.");
                    return erroresFila;
                }

                if (!ValueParsers.TryParseDecimalFlexible(importeTexto, out var importeArchivo))
                {
                    erroresFila.Add($"El campo (Importe) '{importeTexto}' no es un valor valido.");
                    return erroresFila;
                }

                var diferencia = Math.Abs(importeArchivo - refCatalogo.Importe);
                if (diferencia > 0.01m)
                {
                    erroresFila.Add($"El campo (Importe) '{importeArchivo}' no coincide con la base ({refCatalogo.Importe}).");
                }

                return erroresFila;
            },
            out var rechazadas);

        if (rechazadas > 0)
            log.Info(ValidationLog.ReglaRechazadas(ArchivoNombre.CatalogoServicios, rechazadas, rechazadas + filtrado.Count));
        log.Info(ValidationLog.ListasParaImplementar(ArchivoNombre.CatalogoServicios, filtrado.Count));

        result.DatosCatalogoServiciosValidados = filtrado;
    }

}



