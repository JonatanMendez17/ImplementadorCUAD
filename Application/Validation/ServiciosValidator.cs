using Implementador.Models;
using Implementador.Infrastructure;
using System.Globalization;
using Implementador.Application.Validation.Common;
using Implementador.Application.Validation.Core;

namespace Implementador.Application.Validation;

public sealed class ServiciosValidator : RowValidatorBase
{
    public void Apply(ImplementationValidationResult result, IAppLogger log, ValidationReferenceData? snapshot = null)
    {
        if (result.DatosServiciosValidados.Count == 0)
        {
            return;
        }

        log.Separator();
        var safeSnapshot = snapshot ?? ValidationReferenceData.Empty;
        var entidadesRef = safeSnapshot.EntidadesRef;

        var padronPorSocio = result.DatosPadronValidados
            .Where(f => RowValueReader.TryGetFirstValue(f, out var nro, "Nro Socio") && !string.IsNullOrWhiteSpace(nro))
            .GroupBy(f => RowValueReader.GetFirstValue(f, "Nro Socio").Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var codigosConsumos = result.DatosConsumosValidados
            .Select(f => RowValueReader.GetFirstValue(f, "Codigo Consumo", "Código Consumo").Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var codigosServiciosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var serviciosFiltrados = FilterValidRows(
            ArchivoNombre.ConsumosServicios,
            result.DatosServiciosValidados,
            log,
            (row, rowNumber) =>
            {
            var erroresFila = new List<string>();

            var entidad = RowValueReader.GetFirstValue(row, "Entidad");
            var nroSocio = RowValueReader.GetFirstValue(row, "Nro de Socio", "Nro Socio");
            var cuitServicio = RowValueReader.GetFirstValue(row, "CUIT");
            var beneficioServicio = RowValueReader.GetFirstValue(row, "Nro Beneficio", "Beneficio");
            var codigoConsumo = RowValueReader.GetFirstValue(row, "Codigo Consumo", "Código Consumo");

            if (!entidadesRef.Contains(entidad!.Trim()))
            {
                erroresFila.Add($"El campo (Entidad) '{entidad}' no existe en la base.");
            }

            if (!padronPorSocio.TryGetValue(nroSocio!.Trim(), out var filaPadron))
            {
                erroresFila.Add($"El campo (Nro Socio) '{nroSocio}' no existe o no corresponde al padron.");
            }
            else
            {
                var cuitPadron = RowValueReader.GetFirstValue(filaPadron, "CUIT");
                var beneficioPadron = RowValueReader.GetFirstValue(filaPadron, "Beneficio");

                if (!ValueParsers.EqualsDigitsOnly(cuitServicio, cuitPadron))
                {
                    erroresFila.Add($"El campo (CUIT) no coincide con padron para socio '{nroSocio}'.");
                }

                if (!ValueParsers.EqualsTrimmed(beneficioServicio, beneficioPadron))
                {
                    erroresFila.Add($"El campo (Beneficio) no coincide con padron para socio '{nroSocio}'.");
                }
            }

            var codigoNormalizado = codigoConsumo!.Trim();
            if (!codigosServiciosVistos.Add(codigoNormalizado))
            {
                erroresFila.Add($"El campo (Codigo Consumo) '{codigoConsumo}' se encuentra duplicado en el archivo.");
            }

            if (codigosConsumos.Contains(codigoNormalizado))
            {
                erroresFila.Add($"El campo (Codigo Consumo) '{codigoConsumo}' ya existe en archivo Consumos.");
            }

            return erroresFila;
            },
            out var rechazadas);

        if (rechazadas > 0)
            log.Info(ValidationLog.ReglaRechazadas(ArchivoNombre.ConsumosServicios, rechazadas, rechazadas + serviciosFiltrados.Count));
        log.Info(ValidationLog.ListasParaImplementar(ArchivoNombre.ConsumosServicios, serviciosFiltrados.Count));

        result.DatosServiciosValidados = serviciosFiltrados;
    }

}



