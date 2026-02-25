using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace MigradorCUAD.Models
{
    [Table("Importar_Padron_Socio")]
    public class ImportarPadronSocio
    {
        [Column("Ips_Id")]
        public int Id { get; set; }

        [Column("Ips_Entidad")]
        public string Entidad { get; set; } = null!;

        [Column("Ips_Nro_Socio")]
        public int NroSocio { get; set; }

        [Column("Ips_Documento")]
        public int Documento { get; set; }

        [Column("Ips_Cuit")]
        public long? Cuit { get; set; }   

        [Column("Ips_Nro_Puesto")]
        public int? NroPuesto { get; set; } 

        [Column("Ips_Codigo_Categoria")]
        public string CodigoCategoria { get; set; } = null!;

        [Column("Ips_Fecha_Alta_Socio")]
        public DateTime FechaAltaSocio { get; set; }

        // Campos de proceso (NO se insertan ahora)
        public int? SocIdGenerado { get; set; }
        public bool Procesado { get; set; }
        public DateTime? FechaProceso { get; set; }
        public string? UsuarioProceso { get; set; }
        public string? Observacion { get; set; }
        public bool Ignorar { get; set; }
    }
}