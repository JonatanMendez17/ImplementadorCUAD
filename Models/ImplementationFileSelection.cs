namespace ImplementadorCUAD.Models
{
    public class ImplementationFileSelection
    {
        public string? ArchivoCategorias { get; set; }
        public string? ArchivoPadron { get; set; }
        public string? ArchivoConsumos { get; set; }
        public IReadOnlyList<string>? ArchivosConsumosDetalle { get; set; }
        public string? ArchivoServicios { get; set; }
        public string? ArchivoCatalogoServicios { get; set; }
        /// <summary>
        /// Connection string de la base del empleador seleccionado (destino de importación/limpieza).
        /// </summary>
        public string? TargetConnectionString { get; set; }
    }
}
