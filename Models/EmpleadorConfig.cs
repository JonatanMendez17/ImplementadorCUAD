namespace ImplementadorCUAD.Models
{
    /// <summary>
    /// Empleador definido en configuración, con su connection string para la base de destino.
    /// </summary>
    public class EmpleadorConfig
    {
        public string Nombre { get; set; } = string.Empty;
        /// <summary>
        /// Connection string completo, o null si se usa baseDatos + ConexionEmpleadores.
        /// </summary>
        public string? ConnectionString { get; set; }
        /// <summary>
        /// Nombre de base de data cuando se usa ConexionEmpleadores (opción B).
        /// </summary>
        public string? BaseDatos { get; set; }
    }
}
