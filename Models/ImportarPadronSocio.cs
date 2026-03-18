namespace ImplementadorCUAD.Models
{
    public class ImportarPadronSocio
    {
        public string Entidad { get; set; } = null!;
        public int NroSocio { get; set; }
        public int Documento { get; set; }
        public long? Cuit { get; set; }
        public int? NroPuesto { get; set; }
        public string CodigoCategoria { get; set; } = null!;
        public DateTime FechaAltaSocio { get; set; }
    }
}
