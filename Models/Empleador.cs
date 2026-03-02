using System.ComponentModel.DataAnnotations.Schema;

namespace ImplementadorCUAD.Models
{
    public class Empleador
    {
        public int Id { get; set; }

        [Column("Emr_Id")]
        public int EmrId { get; set; }

        [Column("Emr_Nombre")]
        public string? Nombre { get; set; }

        public override string ToString()
        {
            return Nombre ?? string.Empty;
        }
    }
}
