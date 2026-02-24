using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MigradorCUAD.Models;

namespace MigradorCUAD.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<DatosPadron> DatosPadron { get; set; }
        public DbSet<Empleador> Empleador { get; set; }
        public DbSet<Entidad> Entidades { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Construir configuración leyendo appsettings.json desde la carpeta de ejecución
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // Obtener la cadena de conexión "DefaultConnection"
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                optionsBuilder.UseSqlServer(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DatosPadron>()
                .Property(x => x.Importe)
                .HasColumnType("decimal(18,2)");

            // Configuración para Empleador
            modelBuilder.Entity<Empleador>(entity =>
            {
                entity.ToTable("Empleador");

                entity.Property(e => e.Nombre)
                    .HasMaxLength(150);
            });

            // Configuración para Entidad
            modelBuilder.Entity<Entidad>(entity =>
            {
                entity.ToTable("Entidad");

                entity.Property(e => e.Nombre)
                .HasMaxLength(150);
            });
        }
    }
}
