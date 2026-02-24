using System.ComponentModel.DataAnnotations.Schema;

namespace MigradorCUAD.Models
{
    public class Empleador
    {
        public int Id { get; set; }
        
        // Código de empleador (columna física Emr_Id)
        [Column("Emr_Id")]
        public int EmrId { get; set; }

        // Nombre de empleador (columna física Emr_Nombre)
        [Column("Emr_Nombre")]
        public string? Nombre { get; set; }

    }
}
