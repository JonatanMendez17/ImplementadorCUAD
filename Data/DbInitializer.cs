using System.Linq;
using MigradorCUAD.Models;

namespace TuProyecto.Data
{
    public static class DbInitializer
    {
        public static void Seed(AppDbContext context)
        {
            // Si ya hay datos, no hacer nada
            bool tieneEmpleador = context.Empleador.Any();
            bool tieneEntidades = context.Entidades.Any();

            if (tieneEmpleador || tieneEntidades)
            {
                return;
            }

            var empleadoresIniciales = new[]
            {
                new Empleador { Nombre = "Empleador 1" },
                new Empleador { Nombre = "Empleador 2" }
            };

            var entidadesIniciales = new[]
            {
                new Entidad { Codigo = "ENT1", Nombre = "Entidad A" },
                new Entidad { Codigo = "ENT2", Nombre = "Entidad B" }
            };

            context.Empleador.AddRange(empleadoresIniciales);
            context.Entidades.AddRange(entidadesIniciales);

            context.SaveChanges();
        }
    }
}

