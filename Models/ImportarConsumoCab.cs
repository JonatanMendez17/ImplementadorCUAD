namespace ImplementadorCUAD.Models
{
    public class ImportarConsumoCab
    {
        public string? Entidad { get; set; }
        public int NroSocio { get; set; }
        public long? Cuit { get; set; }
        public long CodigoConsumo { get; set; }
        public int CuotasPendientes { get; set; }
        public decimal MontoDeuda { get; set; }
        public int ConceptoDescuento { get; set; }
    }
}