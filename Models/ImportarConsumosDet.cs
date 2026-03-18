namespace ImplementadorCUAD.Models
{
    public class ImportarConsumosDet
    {
        public string? Entidad { get; set; }
        public int CodigoConsumo { get; set; }
        public int NroCuota { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public decimal Monto { get; set; }
    }
}

