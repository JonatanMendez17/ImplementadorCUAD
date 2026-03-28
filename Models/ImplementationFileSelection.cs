namespace Implementador.Models
{
    public class ImplementationFileSelection
    {
        public IReadOnlyList<string>? ArchivosCategorias { get; set; }
        public IReadOnlyList<string>? ArchivosPadron { get; set; }
        public IReadOnlyList<string>? ArchivosConsumos { get; set; }
        public IReadOnlyList<string>? ArchivosConsumosDetalle { get; set; }
        public IReadOnlyList<string>? ArchivosServicios { get; set; }
        public IReadOnlyList<string>? ArchivosCatalogoServicios { get; set; }

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
