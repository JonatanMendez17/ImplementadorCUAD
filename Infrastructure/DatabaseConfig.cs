using System.Configuration;

namespace ImplementadorCUAD.Infrastructure
{
    public static class DatabaseConfig
    {
        public static string? ConnectionString =>
            ConfigurationManager.ConnectionStrings["ImplementadorCUADDb"]?.ConnectionString;
    }
}
