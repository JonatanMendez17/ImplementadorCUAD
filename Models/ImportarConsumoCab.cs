using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImplementadorCUAD.Models
{
    [Table("Importar_Consumo_Cab")]
    public class ImportarConsumoCab
    {
        [Key]
        [Column("lcc_Id")]
        public int Id { get; set; }

        [Column("lcc_Entidad")]
        public string? Entidad { get; set; }

        [Column("lcc_Nro_Socio")]
        public int NroSocio { get; set; }

        [Column("lcc_Cuit")]
        public long? Cuit { get; set; }

        [Column("lcc_Nro_Puesto")]
        public int? NroPuesto { get; set; }

        [Column("lcc_Codigo_Consumo")]
        public long CodigoConsumo { get; set; }

        [Column("lcc_Cuotas_Pendientes")]
        public int CuotasPendientes { get; set; }

        [Column("lcc_Monto_Deuda")]
        public decimal MontoDeuda { get; set; }

        [Column("lcc_Concepto_Descuento")]
        public int ConceptoDescuento { get; set; }

        [Column("lcc_Con_Id_generado")]
        public int? ConIdGenerado { get; set; }

        [Column("lcc_Procesado")]
        public bool Procesado { get; set; }

        [Column("lcc_Fecha_Proceso")]
        public DateTime? FechaProceso { get; set; }

        [Column("lcc_Usuario_Proceso")]
        public string? UsuarioProceso { get; set; }

        [Column("lcc_Observacion")]
        public string? Observacion { get; set; }

        [Column("lcc_Ignorar")]
        public bool Ignorar { get; set; }
    }
}