using Microsoft.Data.SqlClient;
using MigradorCUAD.Infrastructure;

namespace MigradorCUAD.Data
{
    public class DataRepository
    {
        public static bool TestConnection()
        {
            try
            {
                using var connection = new SqlConnection(ConnectionSettings.ConnectionString);
                connection.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
