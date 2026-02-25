using Microsoft.Data.SqlClient;
using MigradorCUAD.Infrastructure;
using MigradorCUAD.Models;
using System.Globalization;

namespace MigradorCUAD.Data
{
    public class AppDbContext : IDisposable
    {
        private readonly string _connectionString;

        public AppDbContext()
        {
            _connectionString = ConnectionSettings.ConnectionString;
        }

        public void Dispose()
        {
        }

        public void EnsureConnection()
        {
            using var connection = CreateOpenConnection();
            using var command = new SqlCommand("SELECT 1;", connection);
            command.ExecuteScalar();
        }

        public List<Empleador> GetEmpleadores()
        {
            var resultado = new List<Empleador>();

            using var connection = CreateOpenConnection();
            using var command = new SqlCommand(
                "SELECT Emr_Id, Emr_Nombre FROM Empleador ORDER BY Emr_Nombre;",
                connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var emrId = reader.GetInt32(0);
                resultado.Add(new Empleador
                {
                    Id = emrId,
                    EmrId = emrId,
                    Nombre = reader.IsDBNull(1) ? null : reader.GetString(1)
                });
            }

            return resultado;
        }

        public List<Entidad> GetEntidades()
        {
            var resultado = new List<Entidad>();

            using var connection = CreateOpenConnection();
            using var command = new SqlCommand(
                "SELECT Ent_Id, Ent_Nombre FROM Entidad ORDER BY Ent_Nombre;",
                connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var entId = reader.GetInt32(0);
                resultado.Add(new Entidad
                {
                    Id = entId,
                    EntId = entId,
                    Nombre = reader.IsDBNull(1) ? null : reader.GetString(1)
                });
            }

            return resultado;
        }

        public int InsertPadronSocio(IEnumerable<ImportarPadronSocio> registros)
        {
            return ExecuteInsert(
                registros,
                @"INSERT INTO Importar_Padron_Socio
          (
              Ips_Entidad,
              Ips_Nro_Socio,
              Ips_Documento,
              Ips_Cuit,
              Ips_Nro_Puesto,
              Ips_Codigo_Categoria,
              Ips_Fecha_Alta_Socio
          )
          VALUES
          (
              @Entidad,
              @NroSocio,
              @Documento,
              @Cuit,
              @NroPuesto,
              @CodigoCategoria,
              @FechaAltaSocio
          );",
                (registro, command) =>
                {
                    command.Parameters.AddWithValue("@Entidad", registro.Entidad);
                    command.Parameters.AddWithValue("@NroSocio", registro.NroSocio);
                    command.Parameters.AddWithValue("@Documento", registro.Documento);
                    command.Parameters.AddWithValue("@FechaAltaSocio", registro.FechaAltaSocio);

                    command.Parameters.AddWithValue(
                        "@Cuit",
                        (object?)registro.Cuit ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "@NroPuesto",
                        (object?)registro.NroPuesto ?? DBNull.Value);

                    command.Parameters.AddWithValue("@CodigoCategoria", registro.CodigoCategoria);
                });
        }

        public int InsertImportarConsumosDet(IEnumerable<ImportarConsumosDet> registros)
        {
            return ExecuteInsert(
                registros,
                @"INSERT INTO Importar_Consumo_Det
                  (Icd_Entidad, Icd_Codigo_Consumo, Icd_Nro_Cuota, Icd_Fecha_Vencimiento, Icd_Monto)
                  VALUES (@Entidad, @CodigoConsumo, @NroCuota, @FechaVencimiento, @Monto);",
                (registro, command) =>
                {
                    command.Parameters.AddWithValue("@Entidad", (object?)registro.Entidad ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CodigoConsumo", registro.CodigoConsumo);
                    command.Parameters.AddWithValue("@NroCuota", registro.NroCuota);
                    command.Parameters.AddWithValue("@FechaVencimiento", registro.FechaVencimiento);
                    command.Parameters.AddWithValue("@Monto", registro.Monto);
                });
        }

        public int InsertImportarConsumoCab(IEnumerable<ImportarConsumoCab> registros)
        {
            return ExecuteInsert(
                registros,
                @"INSERT INTO Importar_Consumo_Cab
          (lcc_Entidad,
           lcc_Nro_Socio,
           lcc_Cuit,
           lcc_Codigo_Consumo,
           lcc_Cuotas_Pendientes,
           lcc_Monto_Deuda,
           lcc_Concepto_Descuento)
          VALUES
          (@Entidad,
           @NroSocio,
           @Cuit,
           @CodigoConsumo,
           @CuotasPendientes,
           @MontoDeuda,
           @ConceptoDescuento);",
            (registro, command) =>
            {
                command.Parameters.AddWithValue("@Entidad", registro.Entidad);
                command.Parameters.AddWithValue("@NroSocio", registro.NroSocio);
                command.Parameters.AddWithValue("@Cuit", (object?)registro.Cuit ?? DBNull.Value);
                command.Parameters.AddWithValue("@CodigoConsumo", registro.CodigoConsumo);
                command.Parameters.AddWithValue("@CuotasPendientes", registro.CuotasPendientes);
                command.Parameters.AddWithValue("@MontoDeuda", registro.MontoDeuda);
                command.Parameters.AddWithValue("@ConceptoDescuento", registro.ConceptoDescuento);
            });
        }

        public bool ExistsImportedDataForEntidad(string entidad)
        {
            using var connection = CreateOpenConnection();
            using var command = new SqlCommand(
                @"SELECT CASE
                    WHEN EXISTS (SELECT 1 FROM Importar_Padron_Socio WHERE Ips_Entidad = @Entidad)
                      OR EXISTS (SELECT 1 FROM Importar_Consumo_Cab WHERE lcc_Entidad = @Entidad)
                      OR EXISTS (SELECT 1 FROM Importar_Consumo_Det WHERE Icd_Entidad = @Entidad)
                    THEN 1 ELSE 0 END;",
                connection);

            command.Parameters.AddWithValue("@Entidad", entidad);
            var exists = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return exists == 1;
        }

        private int ExecuteInsert<T>(
            IEnumerable<T> registros,
            string sql,
            Action<T, SqlCommand> bindParameters)
        {
            int filasAfectadas = 0;
            using var connection = CreateOpenConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var registro in registros)
                {
                    using var command = new SqlCommand(sql, connection, transaction);
                    bindParameters(registro, command);
                    filasAfectadas += command.ExecuteNonQuery();
                }

                transaction.Commit();
                return filasAfectadas;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private SqlConnection CreateOpenConnection()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("No se encontró la cadena de conexión en Infrastructure/ConnectionSettings.cs.");
            }

            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
