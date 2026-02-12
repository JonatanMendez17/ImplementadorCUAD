using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using MigradorCUAD.Models;
using TuProyecto.Models;

namespace TuProyecto.Data
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

            // Configuración opcional para Empleador
            modelBuilder.Entity<Empleador>()
                .Property(e => e.Nombre)
                .HasMaxLength(150);

            // Configuración opcional para Entidad
            modelBuilder.Entity<Entidad>()
                .Property(e => e.Nombre)
                .HasMaxLength(150);

            modelBuilder.Entity<Entidad>()
                .Property(e => e.Codigo)
                .HasMaxLength(20);
        }
    }
}
