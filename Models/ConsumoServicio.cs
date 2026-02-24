using System.ComponentModel.DataAnnotations.Schema;

namespace MigradorCUAD.Models
{
    [Table("Consumos_Servicios")]
    public class ConsumoServicio
    {
        public int Id { get; set; }

        [Column("Entidad_Cod")]
        public string? EntidadCod { get; set; }

        [Column("Nro_Socio")]
        public int NroSocio { get; set; }

        [Column("Cuit")]
        public long Cuit { get; set; }

        [Column("Nro_Beneficio")]
        public int NroBeneficio { get; set; }

        [Column("Codigo_Consumo")]
        public int CodigoConsumo { get; set; }

        [Column("Importe_Cuota")]
        public decimal ImporteCuota { get; set; }

        [Column("Concepto_Descuento_Id")]
        public int ConceptoDescuentoId { get; set; }
    }
}

