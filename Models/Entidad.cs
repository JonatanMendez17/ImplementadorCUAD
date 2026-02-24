using System.ComponentModel.DataAnnotations.Schema;

namespace MigradorCUAD.Models
{
    public class Entidad
    {
        public int Id { get; set; }

        // Código de empleador (columna física Emr_Id)
        [Column("Ent_Id")]
        public int EntId { get; set; }

        // Nombre de empleador (columna física Emr_Nombre)
        [Column("Ent_Nombre")]
        public string? Nombre { get; set; }
    }
}