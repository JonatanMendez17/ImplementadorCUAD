using System.ComponentModel.DataAnnotations;

namespace TuProyecto.Models
{
    public class DatosPadron
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string? Cuit { get; set; }

        [MaxLength(150)]
        public string? RazonSocial { get; set; }

        public DateTime FechaAlta { get; set; }

        public decimal Importe { get; set; }
    }
}
