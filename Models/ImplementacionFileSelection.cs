using System.Collections.Generic;

namespace ImplementadorCUAD.Models
{
    public class ImplementacionFileSelection
    {
        public string? ArchivoCategorias { get; set; }
        public string? ArchivoPadron { get; set; }
        public string? ArchivoConsumos { get; set; }
        public IReadOnlyList<string>? ArchivosConsumosDetalle { get; set; }
        public string? ArchivoServicios { get; set; }
        public string? ArchivoCatalogoServicios { get; set; }
    }
}
