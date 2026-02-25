using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MigradorCUAD.Models
{
    [Table("Importar_Consumo_Det")]
    public class ImportarConsumosDet
    {
        [Key]
        [Column("Icd_Id")]
        public int Icd_Id { get; set; }

        [Column("Icd_Entidad")]
        public string? Entidad { get; set; }

        [Column("Icd_Codigo_Consumo")]
        public int CodigoConsumo { get; set; }

        [Column("Icd_Nro_Cuota")]
        public int NroCuota { get; set; }

        [Column("Icd_Fecha_Vencimiento")]
        public DateTime FechaVencimiento { get; set; }

        [Column("Icd_Monto")]
        public decimal Monto { get; set; }
    }
}

