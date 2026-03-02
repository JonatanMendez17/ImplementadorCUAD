using System.ComponentModel.DataAnnotations.Schema;

namespace ImplementadorCUAD.Models
{
    public class Entidad
    {
        public int Id { get; set; }

        [Column("Ent_Id")]
        public int EntId { get; set; }

        [Column("Ent_Nombre")]
        public string? Nombre { get; set; }

        public override string ToString()
        {
            return Nombre ?? string.Empty;
        }
    }
}
