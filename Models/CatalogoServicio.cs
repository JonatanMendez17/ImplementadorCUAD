using System.ComponentModel.DataAnnotations.Schema;

namespace MigradorCUAD.Models
{
    [Table("Catalogo_Servicios")]
    public class CatalogoServicio
    {
        public int Id { get; set; }

        [Column("Entidad_Cod")]
        public string? EntidadCod { get; set; }

        [Column("Servicio_Nombre")]
        public string? ServicioNombre { get; set; }

        [Column("Importe")]
        public decimal Importe { get; set; }

        [Column("Servicio_Descripcion")]
        public string? ServicioDescripcion { get; set; }
    }
}

