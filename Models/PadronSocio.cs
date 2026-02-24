using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace MigradorCUAD.Models
{
    [Table("Padron_socios")]
    public class PadronSocio
    {
        public int Id { get; set; }

        [Column("Entidad_Cod")]
        public string? EntidadCod { get; set; }

        [Column("Nro_Socio")]
        public int NroSocio { get; set; }

        [Column("Fecha_Alta_Socio")]
        public DateTime FechaAltaSocio { get; set; }

        [Column("Documento")]
        public int Documento { get; set; }

        [Column("Cuit")]
        public long Cuit { get; set; }

        [Column("Beneficio")]
        public int Beneficio { get; set; }

        [Column("Codigo_Categoria")]
        public string? CodigoCategoria { get; set; }
    }
}

