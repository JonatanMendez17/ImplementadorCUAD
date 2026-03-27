using Implementador.Models;
using Implementador.Infrastructure;
using System.Globalization;
using Implementador.Application.Validation.Common;
using Implementador.Application.Validation.Core;

namespace Implementador.Application.Validation;

public sealed class ConsumosValidator : RowValidatorBase
{
    public void Apply(ImplementationValidationResult result, IAppLogger log, ValidationReferenceData? snapshot = null)
    {
        if (result.DatosConsumosValidados.Count == 0)
        {
            return;
        }

        var safeSnapshot = snapshot ?? ValidationReferenceData.Empty;
        var entidadesRef = safeSnapshot.EntidadesRef;
        var conceptosDescuentoVigentes = safeSnapshot.ConceptosDescuentoVigentes;

        var padronPorSocio = result.DatosPadronValidados
            .Where(f => RowValueReader.TryGetFirstValue(f, out var nro, "Nro Socio") && !string.IsNullOrWhiteSpace(nro))
            .GroupBy(f => RowValueReader.GetFirstValue(f, "Nro Socio").Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var codigosConsumoVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var consumosFiltrados = FilterValidRows(
            "Consumos",
            result.DatosConsumosValidados,
            log,
            (row, rowNumber) =>
            {
            var erroresFila = new List<string>();

            var entidad = RowValueReader.GetFirstValue(row, "Entidad");
            var nroSocio = RowValueReader.GetFirstValue(row, "Nro Socio");
            var cuitConsumo = RowValueReader.GetFirstValue(row, "CUIT");
            var beneficioConsumo = RowValueReader.GetFirstValue(row, "Beneficio");
            var codigoConsumo = RowValueReader.GetFirstValue(row, "Codigo Consumo", "Código Consumo");
            var conceptoDescuentoText = RowValueReader.GetFirstValue(row, "Concepto Descuento");

            if (string.IsNullOrWhiteSpace(entidad) || !entidadesRef.Contains(entidad.Trim()))
            {
                erroresFila.Add($"La entidad '{entidad}' no existe en la base.");
            }

            if (string.IsNullOrWhiteSpace(nroSocio) || !padronPorSocio.TryGetValue(nroSocio.Trim(), out var filaPadron))
            {
                erroresFila.Add($"El nro socio '{nroSocio}' no existe o no corresponde al padron.");
            }
            else
            {
                var cuitPadron = RowValueReader.GetFirstValue(filaPadron, "CUIT");
                var beneficioPadron = RowValueReader.GetFirstValue(filaPadron, "Beneficio");

                if (!ValueParsers.EqualsDigitsOnly(cuitConsumo, cuitPadron))
                {
                    erroresFila.Add($"El CUIT no coincide con padron para socio '{nroSocio}'.");
                }

                if (!ValueParsers.EqualsTrimmed(beneficioConsumo, beneficioPadron))
                {
                    erroresFila.Add($"El Beneficio no coincide con padron para socio '{nroSocio}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(codigoConsumo))
            {
                erroresFila.Add("El campo 'codigo consumo' se encuentra vacio.");
            }
            else if (!codigosConsumoVistos.Add(codigoConsumo.Trim()))
            {
                erroresFila.Add($"El codigo de consumo '{codigoConsumo}' se encuentra repetido.");
            }

            if (!string.IsNullOrWhiteSpace(entidad) && !string.IsNullOrWhiteSpace(conceptoDescuentoText) &&
                conceptosDescuentoVigentes.Count > 0)
            {
                var keyConcepto = $"{entidad.Trim()}|{conceptoDescuentoText.Trim()}";
                if (!conceptosDescuentoVigentes.Contains(keyConcepto))
                {
                    erroresFila.Add($"El concepto de descuento '{conceptoDescuentoText}' no existe como código de descuento vigente en la base para la entidad '{entidad?.Trim()}'.");
                }
            }

            return erroresFila;
            },
            out var rechazadas);

        if (rechazadas > 0)
        {
            log.Info($"Resumen validacion Consumos: aceptadas={consumosFiltrados.Count}, rechazadas={rechazadas}.");
        }

        result.DatosConsumosValidados = consumosFiltrados;
    }

}



