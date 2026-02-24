using Microsoft.Data.SqlClient;
using MigradorCUAD.Models;
using MigradorCUAD.Services;

namespace MigradorCUAD.Data
{
    public class AppDbContext : IDisposable
    {
        private readonly string _connectionString;

        public AppDbContext()
        {
            _connectionString = new ConfiguracionAppService().ObtenerConnectionString();
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

        public int InsertPadronSocios(IEnumerable<PadronSocio> registros)
        {
            return ExecuteInsert(
                registros,
                @"INSERT INTO Padron_socios
                  (Entidad_Cod, Nro_Socio, Fecha_Alta_Socio, Documento, Cuit, Beneficio, Codigo_Categoria)
                  VALUES (@EntidadCod, @NroSocio, @FechaAltaSocio, @Documento, @Cuit, @Beneficio, @CodigoCategoria);",
                (registro, command) =>
                {
                    command.Parameters.AddWithValue("@EntidadCod", (object?)registro.EntidadCod ?? DBNull.Value);
                    command.Parameters.AddWithValue("@NroSocio", registro.NroSocio);
                    command.Parameters.AddWithValue("@FechaAltaSocio", registro.FechaAltaSocio);
                    command.Parameters.AddWithValue("@Documento", registro.Documento);
                    command.Parameters.AddWithValue("@Cuit", registro.Cuit);
                    command.Parameters.AddWithValue("@Beneficio", registro.Beneficio);
                    command.Parameters.AddWithValue("@CodigoCategoria", (object?)registro.CodigoCategoria ?? DBNull.Value);
                });
        }

        public int InsertCategoriasSocio(IEnumerable<CategoriaSocio> registros)
        {
            return ExecuteInsert(
                registros,
                @"INSERT INTO Categorias_Socio
                  (Entidad_Cod, Cat_Codigo, Cat_Nombre, Cat_Descripcion, Es_Predeterminada, Monto_CS, Concepto_Descuento_Id)
                  VALUES (@EntidadCod, @CatCodigo, @CatNombre, @CatDescripcion, @EsPredeterminada, @MontoCS, @ConceptoDescuentoId);",
                (registro, command) =>
                {
                    command.Parameters.AddWithValue("@EntidadCod", (object?)registro.EntidadCod ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CatCodigo", (object?)registro.CatCodigo ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CatNombre", (object?)registro.CatNombre ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CatDescripcion", (object?)registro.CatDescripcion ?? DBNull.Value);
                    command.Parameters.AddWithValue("@EsPredeterminada", registro.EsPredeterminada);
                    command.Parameters.AddWithValue("@MontoCS", registro.MontoCS);
                    command.Parameters.AddWithValue("@ConceptoDescuentoId", registro.ConceptoDescuentoId);
                });
        }

        public int InsertImportarConsumosDetalle(IEnumerable<ImportarConsumosDetalle> registros)
        {
            return ExecuteInsert(
                registros,
                @"INSERT INTO Importar_Consumos_Detalle
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

        public int InsertCatalogoServicios(IEnumerable<CatalogoServicio> registros)
        {
            return ExecuteInsert(
                registros,
                @"INSERT INTO Catalogo_Servicios
                  (Entidad_Cod, Servicio_Nombre, Importe, Servicio_Descripcion)
                  VALUES (@EntidadCod, @ServicioNombre, @Importe, @ServicioDescripcion);",
                (registro, command) =>
                {
                    command.Parameters.AddWithValue("@EntidadCod", (object?)registro.EntidadCod ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ServicioNombre", (object?)registro.ServicioNombre ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Importe", registro.Importe);
                    command.Parameters.AddWithValue("@ServicioDescripcion", (object?)registro.ServicioDescripcion ?? DBNull.Value);
                });
        }

        public int InsertConsumosServicios(IEnumerable<ConsumoServicio> registros)
        {
            return ExecuteInsert(
                registros,
                @"INSERT INTO Consumos_Servicios
                  (Entidad_Cod, Nro_Socio, Cuit, Nro_Beneficio, Codigo_Consumo, Importe_Cuota, Concepto_Descuento_Id)
                  VALUES (@EntidadCod, @NroSocio, @Cuit, @NroBeneficio, @CodigoConsumo, @ImporteCuota, @ConceptoDescuentoId);",
                (registro, command) =>
                {
                    command.Parameters.AddWithValue("@EntidadCod", (object?)registro.EntidadCod ?? DBNull.Value);
                    command.Parameters.AddWithValue("@NroSocio", registro.NroSocio);
                    command.Parameters.AddWithValue("@Cuit", registro.Cuit);
                    command.Parameters.AddWithValue("@NroBeneficio", registro.NroBeneficio);
                    command.Parameters.AddWithValue("@CodigoConsumo", registro.CodigoConsumo);
                    command.Parameters.AddWithValue("@ImporteCuota", registro.ImporteCuota);
                    command.Parameters.AddWithValue("@ConceptoDescuentoId", registro.ConceptoDescuentoId);
                });
        }

        public int InsertConsumosImportados(IEnumerable<ConsumoImportado> registros)
        {
            return ExecuteInsert(
                registros,
                @"INSERT INTO Consumo
                  (Entidad_Cod, Nro_Socio, Cuit, Beneficio, Codigo_Consumo, Cuotas_Pendientes, Monto_Deuda, Concepto_Descuento_Id)
                  VALUES (@EntidadCod, @NroSocio, @Cuit, @Beneficio, @CodigoConsumo, @CuotasPendientes, @MontoDeuda, @ConceptoDescuentoId);",
                (registro, command) =>
                {
                    command.Parameters.AddWithValue("@EntidadCod", (object?)registro.EntidadCod ?? DBNull.Value);
                    command.Parameters.AddWithValue("@NroSocio", registro.NroSocio);
                    command.Parameters.AddWithValue("@Cuit", registro.Cuit);
                    command.Parameters.AddWithValue("@Beneficio", registro.Beneficio);
                    command.Parameters.AddWithValue("@CodigoConsumo", registro.CodigoConsumo);
                    command.Parameters.AddWithValue("@CuotasPendientes", registro.CuotasPendientes);
                    command.Parameters.AddWithValue("@MontoDeuda", registro.MontoDeuda);
                    command.Parameters.AddWithValue("@ConceptoDescuentoId", registro.ConceptoDescuentoId);
                });
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
                throw new InvalidOperationException("No se encontró la cadena de conexión en appsettings.json.");
            }

            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
