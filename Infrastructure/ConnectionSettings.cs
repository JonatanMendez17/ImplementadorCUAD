namespace ImplementadorCUAD.Infrastructure
{
    public static class ConnectionSettings
    {
        // Modificar este valor para conectar a otra base de datos.
        public static string ConnectionString { get; set; } =
            "Server=(localdb)\\MSSQLLocalDB;Database=ImplementadorCUAD_DB;Trusted_Connection=True;MultipleActiveResultSets=true";
    }
}
