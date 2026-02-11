using System;
using System.Collections.Generic;
using System.Linq;
using MigradorCUAD.Models;

namespace MigradorCUAD.Services
{
    public static class CrossValidator
    {
        public static List<string> Validate(
            List<Socio> socios,
            List<Consumo> consumos,
            List<ConsumoDetalle> detalles,
            List<Servicio> servicios)
        {
            var errores = new List<string>();

            errores.AddRange(ValidarConsumos(socios, consumos));
            errores.AddRange(ValidarDetalles(consumos, detalles));
            errores.AddRange(ValidarServicios(socios, servicios));

            return errores;
        }

        private static List<string> ValidarConsumos(
            List<Socio> socios,
            List<Consumo> consumos)
         {
            var errores = new List<string>();

            // Duplicados
            var duplicados = consumos
                .GroupBy(c => c.NumeroConsumo)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var nro in duplicados)
                errores.Add($"Consumo duplicado: {nro}");

            // Socio inexistente
            foreach (var consumo in consumos)
            {
                if (!socios.Any(s => s.NumeroSocio == consumo.NumeroSocio))
                {
                    errores.Add($"El socio {consumo.NumeroSocio} no existe para el consumo {consumo.NumeroConsumo}");
                }
            }

            return errores;
        }

        private static List<string> ValidarDetalles(
            List<Consumo> consumos,
            List<ConsumoDetalle> detalles)
        {
            var errores = new List<string>();

            foreach (var detalle in detalles)
            {
                if (!consumos.Any(c => c.NumeroConsumo == detalle.NumeroConsumo))
                {
                    errores.Add($"Detalle con consumo inexistente: {detalle.NumeroConsumo}");
                }

                if (detalle.PrimerVencimiento < DateTime.Today)
                {
                    errores.Add($"Primer vencimiento inválido en consumo {detalle.NumeroConsumo}");
                }
            }

            return errores;
        }

        private static List<string> ValidarServicios(
            List<Socio> socios,
            List<Servicio> servicios)
        {
            var errores = new List<string>();

            foreach (var servicio in servicios)
            {
                if (!socios.Any(s => s.NumeroSocio == servicio.NumeroSocio))
                {
                    errores.Add($"Servicio con socio inexistente: {servicio.NumeroSocio}");
                }
            }

            return errores;
        }
    }
}
