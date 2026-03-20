using ImplementadorCUAD.Models;
using ImplementadorCUAD.Infrastructure;
using System.Globalization;

namespace ImplementadorCUAD.Services;

public sealed class ConsumosValidator
{
    private readonly IAppDbContextFactory _dbContextFactory;

    public ConsumosValidator(IAppDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public void Apply(ImplementationValidationResult result, Action<string> log)
    {
        if (result.DatosConsumosValidados.Count == 0)
        {
            return;
        }

        HashSet<string> entidadesCuad;
        HashSet<string> conceptosDescuentoVigentes;
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

            conceptosDescuentoVigentes = db.GetConceptosDescuentoVigentesParaConsumos();
        }
        catch (Exception ex)
        {
            log($"Consumos: No se pudo validar entidades de CUAD. {ex.Message}");
            result.DatosConsumosValidados = new List<Dictionary<string, string>>();
            conceptosDescuentoVigentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var padronPorSocio = result.DatosPadronValidados
            .Where(f => TryGetFirstValue(f, out var nro, "Nro Socio") && !string.IsNullOrWhiteSpace(nro))
            .GroupBy(f => GetFirstValue(f, "Nro Socio").Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var consumosFiltrados = new List<Dictionary<string, string>>();
        var codigosConsumoVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rechazadas = 0;

        for (int i = 0; i < result.DatosConsumosValidados.Count; i++)
        {
            var row = result.DatosConsumosValidados[i];
            var rowNumber = i + 2;
            var erroresFila = new List<string>();

            var entidad = GetFirstValue(row, "Entidad");
            var nroSocio = GetFirstValue(row, "Nro Socio");
            var cuitConsumo = GetFirstValue(row, "CUIT");
            var beneficioConsumo = GetFirstValue(row, "Beneficio");
            var codigoConsumo = GetFirstValue(row, "Codigo Consumo", "Código Consumo");
            var conceptoDescuentoText = GetFirstValue(row, "Concepto Descuento");

            if (string.IsNullOrWhiteSpace(entidad) || !entidadesCuad.Contains(entidad.Trim()))
            {
                erroresFila.Add($"La entidad '{entidad}' no existe en CUAD.");
            }

            if (string.IsNullOrWhiteSpace(nroSocio) || !padronPorSocio.TryGetValue(nroSocio.Trim(), out var filaPadron))
            {
                erroresFila.Add($"El nro socio '{nroSocio}' no existe o no corresponde al padron.");
            }
            else
            {
                var cuitPadron = GetFirstValue(filaPadron, "CUIT");
                var beneficioPadron = GetFirstValue(filaPadron, "Beneficio");

                if (!EqualsDigitsOnly(cuitConsumo, cuitPadron))
                {
                    erroresFila.Add($"El CUIT no coincide con padron para socio '{nroSocio}'.");
                }

                if (!EqualsTrimmed(beneficioConsumo, beneficioPadron))
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
                    erroresFila.Add($"El concepto de descuento '{conceptoDescuentoText}' no existe como código de descuento vigente en CUAD para la entidad '{entidad?.Trim()}'.");
                }
            }

            if (erroresFila.Count == 0)
            {
                consumosFiltrados.Add(row);
            }
            else
            {
                rechazadas++;
                log($"Consumos row {rowNumber}: {string.Join(" | ", erroresFila)}");
            }
        }

        if (rechazadas > 0)
        {
            log($"Resumen validacion Consumos: aceptadas={consumosFiltrados.Count}, rechazadas={rechazadas}.");
        }

        result.DatosConsumosValidados = consumosFiltrados;
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

    private static string GetFirstValue(Dictionary<string, string> row, params string[] posiblesClaves)
    {
        return TryGetFirstValue(row, out var value, posiblesClaves) ? value : string.Empty;
    }

    private static bool EqualsTrimmed(string? left, string? right)
    {
        var a = (left ?? string.Empty).Trim();
        var b = (right ?? string.Empty).Trim();
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EqualsDigitsOnly(string? left, string? right)
    {
        static string Digits(string? text) => new string((text ?? string.Empty).Where(char.IsDigit).ToArray());
        return string.Equals(Digits(left), Digits(right), StringComparison.Ordinal);
    }
}

