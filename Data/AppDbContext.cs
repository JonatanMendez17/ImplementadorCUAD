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
        public DbSet<CategoriaSocio> CategoriasSocio { get; set; }
        public DbSet<ImportarConsumosDetalle> ImportarConsumosDetalle { get; set; }
        public DbSet<CatalogoServicio> CatalogoServicios { get; set; }
        public DbSet<ConsumoServicio> ConsumosServicios { get; set; }
        public DbSet<ConsumoImportado> ConsumosImportados { get; set; }
        public DbSet<PadronSocio> PadronSocios { get; set; }

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

            // Configuración para Categorias_Socio
            modelBuilder.Entity<CategoriaSocio>(entity =>
            {
                entity.ToTable("Categorias_Socio");

                entity.Property(e => e.EntidadCod)
                    .HasMaxLength(10);

                entity.Property(e => e.CatCodigo)
                    .HasMaxLength(10);

                entity.Property(e => e.CatNombre)
                    .HasMaxLength(50);

                entity.Property(e => e.CatDescripcion)
                    .HasMaxLength(150);

                entity.Property(e => e.MontoCS)
                    .HasColumnType("decimal(10,2)");
            });

            // Configuración para Importar_Consumos_Detalle
            modelBuilder.Entity<ImportarConsumosDetalle>(entity =>
            {
                entity.ToTable("Importar_Consumos_Detalle");

                entity.Property(e => e.Entidad)
                    .HasMaxLength(10);

                entity.Property(e => e.Monto)
                    .HasColumnType("decimal(14,2)");
            });

            // Configuración para Catalogo_Servicios
            modelBuilder.Entity<CatalogoServicio>(entity =>
            {
                entity.ToTable("Catalogo_Servicios");

                entity.Property(e => e.EntidadCod)
                    .HasMaxLength(10);

                entity.Property(e => e.ServicioNombre)
                    .HasMaxLength(100);

                entity.Property(e => e.ServicioDescripcion)
                    .HasMaxLength(200);

                entity.Property(e => e.Importe)
                    .HasColumnType("decimal(10,2)");
            });

            // Configuración para Consumos_Servicios
            modelBuilder.Entity<ConsumoServicio>(entity =>
            {
                entity.ToTable("Consumos_Servicios");

                entity.Property(e => e.EntidadCod)
                    .HasMaxLength(10);

                entity.Property(e => e.ImporteCuota)
                    .HasColumnType("decimal(10,2)");
            });

            // Configuración para Consumo (tabla de deuda)
            modelBuilder.Entity<ConsumoImportado>(entity =>
            {
                entity.ToTable("Consumo");

                entity.Property(e => e.EntidadCod)
                    .HasMaxLength(10);

                entity.Property(e => e.MontoDeuda)
                    .HasColumnType("decimal(14,2)");
            });

            // Configuración para Padron_socios
            modelBuilder.Entity<PadronSocio>(entity =>
            {
                entity.ToTable("Padron_socios");

                entity.Property(e => e.EntidadCod)
                    .HasMaxLength(10);

                entity.Property(e => e.CodigoCategoria)
                    .HasMaxLength(10);
            });
        }
    }
}
