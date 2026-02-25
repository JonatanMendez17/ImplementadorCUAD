namespace MigradorCUAD.Models
{
    public class MigrationValidationResult
    {
        public List<Dictionary<string, string>> DatosPadronValidados { get; set; } = new();
        public List<Dictionary<string, string>> DatosCategoriasValidadas { get; set; } = new();
        public List<Dictionary<string, string>> DatosConsumosValidados { get; set; } = new();
        public List<Dictionary<string, string>> DatosConsumosDetalleValidados { get; set; } = new();
        public List<Dictionary<string, string>> DatosCatalogoServiciosValidados { get; set; } = new();
        public List<Dictionary<string, string>> DatosServiciosValidados { get; set; } = new();
        public bool HuboCarga { get; set; }
    }
}
