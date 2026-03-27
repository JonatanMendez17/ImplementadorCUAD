namespace Implementador.Models
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

        /// <summary>
        /// Nombre de la entidad seleccionada en la UI. Si se especifica, se verifica que coincida
        /// con la entidad detectada en los archivos antes de ejecutar las validaciones de negocio.
        /// </summary>
        public string? EntidadEsperada { get; set; }
    }
}

