using System.ComponentModel.DataAnnotations.Schema;

namespace MigradorCUAD.Models
{
    [Table("Consumo")]
    public class ConsumoImportado
    {
        public int Id { get; set; }

        [Column("Entidad_Cod")]
        public string? EntidadCod { get; set; }

        [Column("Nro_Socio")]
        public int NroSocio { get; set; }

        [Column("Cuit")]
        public long Cuit { get; set; }

        [Column("Beneficio")]
        public int Beneficio { get; set; }

        [Column("Codigo_Consumo")]
        public long CodigoConsumo { get; set; }

        [Column("Cuotas_Pendientes")]
        public int CuotasPendientes { get; set; }

        [Column("Monto_Deuda")]
        public decimal MontoDeuda { get; set; }

        [Column("Concepto_Descuento_Id")]
        public int ConceptoDescuentoId { get; set; }
    }
}

