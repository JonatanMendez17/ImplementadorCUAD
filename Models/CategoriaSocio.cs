using System.ComponentModel.DataAnnotations.Schema;

namespace MigradorCUAD.Models
{
    [Table("Categorias_Socio")]
    public class CategoriaSocio
    {
        public int Id { get; set; }

        [Column("Entidad_Cod")]
        public string? EntidadCod { get; set; }

        [Column("Cat_Codigo")]
        public string? CatCodigo { get; set; }

        [Column("Cat_Nombre")]
        public string? CatNombre { get; set; }

        [Column("Cat_Descripcion")]
        public string? CatDescripcion { get; set; }

        [Column("Es_Predeterminada")]
        public bool EsPredeterminada { get; set; }

        [Column("Monto_CS")]
        public decimal MontoCS { get; set; }

        [Column("Concepto_Descuento_Id")]
        public int ConceptoDescuentoId { get; set; }
    }
}

