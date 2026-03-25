using ImplementadorCUAD.Models;
using ImplementadorCUAD.Infrastructure;
using System.Globalization;

namespace ImplementadorCUAD.Services;

public sealed class ServiciosValidator(IAppDbContextFactory dbContextFactory)
{
    private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

    public void Apply(ImplementationValidationResult result, IAppLogger log)
    {
        if (result.DatosServiciosValidados.Count == 0)
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
            log.Error($"Consumos Servicios: no se pudo validar entidades de la base. {ex.Message}");
            result.DatosServiciosValidados = new List<Dictionary<string, string>>();
            return;
        }

        var padronPorSocio = result.DatosPadronValidados
            .Where(f => TryGetFirstValue(f, out var nro, "Nro Socio") && !string.IsNullOrWhiteSpace(nro))
            .GroupBy(f => GetFirstValue(f, "Nro Socio").Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var codigosConsumos = result.DatosConsumosValidados
            .Select(f => GetFirstValue(f, "Codigo Consumo", "Código Consumo").Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var serviciosFiltrados = new List<Dictionary<string, string>>();
        var codigosServiciosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rechazadas = 0;

        for (int i = 0; i < result.DatosServiciosValidados.Count; i++)
        {
            var row = result.DatosServiciosValidados[i];
            var rowNumber = i + 2;
            var erroresFila = new List<string>();

            var entidad = GetFirstValue(row, "Entidad");
            var nroSocio = GetFirstValue(row, "Nro de Socio", "Nro Socio");
            var cuitServicio = GetFirstValue(row, "CUIT");
            var beneficioServicio = GetFirstValue(row, "Nro Beneficio", "Beneficio");
            var codigoConsumo = GetFirstValue(row, "Codigo Consumo", "Código Consumo");

            if (string.IsNullOrWhiteSpace(entidad) || !entidadesCuad.Contains(entidad.Trim()))
            {
                erroresFila.Add($"La entidad '{entidad}' no existe en la base.");
            }

            if (string.IsNullOrWhiteSpace(nroSocio) || !padronPorSocio.TryGetValue(nroSocio.Trim(), out var filaPadron))
            {
                erroresFila.Add($"El socio '{nroSocio}' no existe o no corresponde al padron socios.");
            }
            else
            {
                var cuitPadron = GetFirstValue(filaPadron, "CUIT");
                var beneficioPadron = GetFirstValue(filaPadron, "Beneficio");

                if (!EqualsDigitsOnly(cuitServicio, cuitPadron))
                {
                    erroresFila.Add($"El CUIT no coincide con padron para socio '{nroSocio}'.");
                }

                if (!EqualsTrimmed(beneficioServicio, beneficioPadron))
                {
                    erroresFila.Add($"El Beneficio no coincide con padron para socio '{nroSocio}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(codigoConsumo))
            {
                erroresFila.Add("EL campo 'codigo consumo' se encuentra vacio.");
            }
            else
            {
                var codigoNormalizado = codigoConsumo.Trim();
                if (!codigosServiciosVistos.Add(codigoNormalizado))
                {
                    erroresFila.Add($"El codigo de consumo '{codigoConsumo}' se encuentra repetido en Consumos Servicios.");
                }

                if (codigosConsumos.Contains(codigoNormalizado))
                {
                    erroresFila.Add($"El codigo de consumo '{codigoConsumo}' ya existe en archivo Consumos.");
                }
            }

            if (erroresFila.Count == 0)
            {
                serviciosFiltrados.Add(row);
            }
            else
            {
                rechazadas++;
                log.Warn($"Consumos Servicios row {rowNumber}: {string.Join(" | ", erroresFila)}");
            }
        }

        if (rechazadas > 0)
        {
            log.Info($"Resumen validacion Consumos Servicios: aceptadas={serviciosFiltrados.Count}, rechazadas={rechazadas}.");
        }

        result.DatosServiciosValidados = serviciosFiltrados;
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

