using Microsoft.Data.SqlClient;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Models;
using System.Globalization;

namespace ImplementadorCUAD.Data
{
    public class AppDbContext : IAppDbContext
    {
        private readonly string _connectionString;

        public AppDbContext()
        {
            _connectionString = ConnectionSettings.CuadConnectionString;
        }

        public AppDbContext(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("El connection string de destino no puede ser nulo ni vacío.", nameof(connectionString));
            _connectionString = connectionString;
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

        public List<Entidad> GetEntidad()
        {
            var resultado = new List<Entidad>();

            using var connection = CreateOpenConnection();
            // Las entidades lógicas se obtienen desde la tabla física Mutual (Mut_Nombre, Mut_Alta='S').
            using var command = new SqlCommand(
                @"SELECT Mut_Id,
                         Mut_Nombre
                  FROM Mutual
                  WHERE Mut_Alta = 'S'
                  ORDER BY Mut_Nombre;",
                connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var mutId = reader.GetInt32(0);
                resultado.Add(new Entidad
                {
                    Id = mutId,
                    EntId = mutId,
                    Nombre = reader.IsDBNull(1) ? null : reader.GetString(1)
                });
            }

            return resultado;
        }

        public List<CategoriaCuadRef> GetCategoriasCuad()
        {
            var resultado = new List<CategoriaCuadRef>();

            using var connection = CreateOpenConnection();
            // Las categorias de CUAD se obtienen desde las tablas físicas Mutual y Mutual_Categoria.
            using var command = new SqlCommand(
                @"SELECT 
                         mc.Mca_Id,
                         m.Mut_Nombre,
                         mc.Mca_Nome,
                         mc.Mca_Vigente
                  FROM Mutual m
                  INNER JOIN Mutual_Categoria mc 
                      ON m.Mut_Id = mc.Mut_Id
                  WHERE m.Mut_Alta = 'S'
                    AND mc.Mca_Vigente = 1;",
                connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                resultado.Add(new CategoriaCuadRef
                {
                    Id = reader.GetInt32(0),
                    Entidad = reader.GetString(1),
                    CodigoCategoria = reader.GetString(2),
                    NombreCategoria = reader.IsDBNull(2) ? null : reader.GetString(2),
                    EsPredeterminada = false,
                    Habilitada = !reader.IsDBNull(3) && reader.GetBoolean(3)
                });
            }

            return resultado;
        }

        /// <summary>
        /// Devuelve las combinaciones (Entidad, CodigoCategoria) que tienen código de cuota social vigente en CUAD.
        /// La información se obtiene desde las tablas físicas Mutual, Mutual_Categoria y Mutual_Categoria_Codigo.
        /// </summary>
        public HashSet<string> GetCategoriasConCuotaSocialVigente()
        {
            var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var connection = CreateOpenConnection();
            using var command = new SqlCommand(
                @"SELECT 
                         m.Mut_Nombre,
                         mc.Mca_Nome
                  FROM Mutual m
                  INNER JOIN Mutual_Categoria mc 
                      ON m.Mut_Id = mc.Mut_Id
                  INNER JOIN Mutual_Categoria_Codigo mcc
                      ON mc.Mca_Id = mcc.Mca_Id
                  WHERE m.Mut_Alta = 'S'
                    AND mc.Mca_Vigente = 1
                    AND mcc.Mcc_Vigente = 1
                    AND mcc.Mcc_COD_Entidad = 6619;",
                connection);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var entidad = reader.IsDBNull(0) ? null : reader.GetString(0);
                var categoria = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (string.IsNullOrWhiteSpace(entidad) || string.IsNullOrWhiteSpace(categoria))
                {
                    continue;
                }

                var key = $"{entidad.Trim()}|{categoria.Trim()}";
                resultado.Add(key);
            }

            return resultado;
        }

        /// <summary>
        /// Devuelve las combinaciones (Entidad, ConceptoDescuento) que tienen código de descuento vigente
        /// para consumos en CUAD. La información se obtiene desde las tablas físicas Mutual y Mutual_Servicio_Empleador.
        /// </summary>
        public HashSet<string> GetConceptosDescuentoVigentesParaConsumos()
        {
            var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var connection = CreateOpenConnection();
            using var command = new SqlCommand(
                @"SELECT 
                         m.Mut_Nombre,
                         mse.Mse_COD_entidad
                  FROM Mutual m
                  INNER JOIN Mutual_Servicio_Empleador mse 
                      ON m.Mut_Id = mse.Mut_Id
                  WHERE m.Mut_Alta = 'S'
                    AND mse.Mse_Vigente = 1;",
                connection);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var entidad = reader.IsDBNull(0) ? null : reader.GetString(0);
                var conceptoValor = reader.IsDBNull(1) ? null : reader.GetValue(1)?.ToString();
                if (string.IsNullOrWhiteSpace(entidad) || string.IsNullOrWhiteSpace(conceptoValor))
                {
                    continue;
                }

                var key = $"{entidad.Trim()}|{conceptoValor.Trim()}";
                resultado.Add(key);
            }

            return resultado;
        }

        public bool TryGetEmrIdByEmpleadoCodigoYDocumento(string empleadoCodigo, long documento, out int emrId)
        {
            emrId = 0;

            if (string.IsNullOrWhiteSpace(empleadoCodigo) || documento <= 0)
            {
                return false;
            }

            using var connection = CreateOpenConnection();
            using var command = new SqlCommand(
                @"SELECT TOP 1 e.Emr_Id
                  FROM Empleado e
                  INNER JOIN Persona p ON p.Per_Id = e.Per_Id
                  WHERE e.Emp_Cod = @EmpCod
                    AND p.Per_NroDoc = @PerNroDoc;",
                connection);

            command.Parameters.AddWithValue("@EmpCod", empleadoCodigo.Trim());
            command.Parameters.AddWithValue("@PerNroDoc", documento);

            var result = command.ExecuteScalar();
            if (result == null || result == DBNull.Value)
            {
                return false;
            }

            emrId = Convert.ToInt32(result, CultureInfo.InvariantCulture);
            return true;
        }

        public List<CatalogoServicioCuadRef> GetCatalogoServiciosCuad()
        {
            var resultado = new List<CatalogoServicioCuadRef>();

            using var connection = CreateOpenConnection();
            using var command = new SqlCommand(
                @"SELECT Id,
                         Entidad,
                         Servicio,
                         Importe,
                         CodigoConceptoDescuento,
                         Habilitado
                  FROM CatalogoServiciosCuad
                  WHERE Habilitado = 1;",
                connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                resultado.Add(new CatalogoServicioCuadRef
                {
                    Id = reader.GetInt32(0),
                    Entidad = reader.GetString(1),
                    Servicio = reader.GetString(2),
                    Importe = reader.GetDecimal(3),
                    CodigoConceptoDescuento = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Habilitado = !reader.IsDBNull(5) && reader.GetBoolean(5)
                });
            }

            return resultado;
        }

        public Task<int> InsertPadronSocioAsync(IReadOnlyList<ImportarPadronSocio> registros, IProgress<int>? progress = null)
        {
            return ExecuteInsertAsync(
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
                        registro.NroPuesto ?? 0);

                    command.Parameters.AddWithValue("@CodigoCategoria", registro.CodigoCategoria);
                },
                progress);
        }

        public Task<int> InsertImportarConsumosDetAsync(IReadOnlyList<ImportarConsumosDet> registros, IProgress<int>? progress = null)
        {
            return ExecuteInsertAsync(
                registros,
                @"INSERT INTO Importar_Consumo_Det
                (
                    Icd_Entidad, 
                    Icd_Codigo_Consumo, 
                    Icd_Nro_Cuota, 
                    Icd_Fecha_Vencimiento, 
                    Icd_Monto
                )
                VALUES 
                (
                    @Entidad, 
                    @CodigoConsumo, 
                    @NroCuota, 
                    @FechaVencimiento, 
                    @Monto
                );",
                (registro, command) =>
                {
                    command.Parameters.AddWithValue("@Entidad", (object?)registro.Entidad ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CodigoConsumo", registro.CodigoConsumo);
                    command.Parameters.AddWithValue("@NroCuota", registro.NroCuota);
                    command.Parameters.AddWithValue("@FechaVencimiento", registro.FechaVencimiento);
                    command.Parameters.AddWithValue("@Monto", registro.Monto);
                },
                progress);
        }

        public Task<int> InsertImportarConsumoCabAsync(IReadOnlyList<ImportarConsumoCab> registros, IProgress<int>? progress = null)
        {
            return ExecuteInsertAsync(
                registros,
                @"INSERT INTO Importar_Consumo_Cab
                (
                    Icc_Entidad,
                    Icc_Nro_Socio,
                    Icc_Cuit,
                    Icc_Codigo_Consumo,
                    Icc_Cuotas_Pendientes,
                    Icc_Monto_Deuda,
                    Icc_Concepto_Descuento
                )
                VALUES
                (
                    @Entidad,
                    @NroSocio,
                    @Cuit,
                    @CodigoConsumo,
                    @CuotasPendientes,
                    @MontoDeuda,
                    @ConceptoDescuento
                );",
            (registro, command) =>
            {
                command.Parameters.AddWithValue("@Entidad", registro.Entidad);
                command.Parameters.AddWithValue("@NroSocio", registro.NroSocio);
                command.Parameters.AddWithValue("@Cuit", (object?)registro.Cuit ?? DBNull.Value);
                command.Parameters.AddWithValue("@CodigoConsumo", registro.CodigoConsumo);
                command.Parameters.AddWithValue("@CuotasPendientes", registro.CuotasPendientes);
                command.Parameters.AddWithValue("@MontoDeuda", registro.MontoDeuda);
                command.Parameters.AddWithValue("@ConceptoDescuento", registro.ConceptoDescuento);
            },
            progress);
        }

        private async Task<int> ExecuteInsertAsync<T>(IReadOnlyList<T> registros,string sql, Action<T, SqlCommand> bindParameters, IProgress<int>? progress = null)
        {
            if (registros.Count == 0)
            {
                return 0;
            }

            int filasAfectadas = 0;
            using var connection = CreateOpenConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                using (var command = new SqlCommand(sql, connection, transaction))
                {
                    foreach (var registro in registros)
                    {
                        command.Parameters.Clear();
                        bindParameters(registro, command);
                        filasAfectadas += await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        if (progress is not null)
                        {
                            progress.Report(1);
                        }
                    }
                }

                await Task.Run(() => transaction.Commit()).ConfigureAwait(false);
                return filasAfectadas;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }


        public bool ExistsImportedDataForEntidad(string entidad)
        {
            using var connection = CreateOpenConnection();
            using var command = new SqlCommand(
                @"SELECT CASE
                    WHEN EXISTS (SELECT 1 FROM Importar_Padron_Socio WHERE Ips_Entidad = @Entidad)
                      OR EXISTS (SELECT 1 FROM Importar_Consumo_Cab WHERE Icc_Entidad = @Entidad)
                      OR EXISTS (SELECT 1 FROM Importar_Consumo_Det WHERE Icd_Entidad = @Entidad)
                    THEN 1 ELSE 0 END;",
                connection);

            command.Parameters.AddWithValue("@Entidad", entidad);
            var exists = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return exists == 1;
        }

        public (int Padron, int ConsumoCab, int ConsumoDet) DeleteImportedDataForEntidad(string entidadNombre, int entidadId)
        {
            if (string.IsNullOrWhiteSpace(entidadNombre) && entidadId <= 0)
            {
                throw new ArgumentException("Debe informar una entidad valida para eliminar data.");
            }

            var entidadNombreNormalizada = (entidadNombre ?? string.Empty).Trim();
            var entidadIdTexto = entidadId.ToString(CultureInfo.InvariantCulture);

            using var connection = CreateOpenConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                int ExecuteDelete(string sql)
                {
                    using var command = new SqlCommand(sql, connection, transaction);
                    command.Parameters.AddWithValue("@EntidadNombre", entidadNombreNormalizada);
                    command.Parameters.AddWithValue("@EntidadId", entidadIdTexto);
                    return command.ExecuteNonQuery();
                }

                var eliminadosConsumoDet = ExecuteDelete(
                    @"DELETE FROM Importar_Consumo_Det
                            WHERE Icd_Entidad = @EntidadNombre 
                               OR Icd_Entidad = @EntidadId;");

                var eliminadosConsumoCab = ExecuteDelete(
                    @"DELETE FROM Importar_Consumo_Cab
                            WHERE Icc_Entidad = @EntidadNombre 
                               OR Icc_Entidad = @EntidadId;");

                var eliminadosPadron = ExecuteDelete(
                    @"DELETE FROM Importar_Padron_Socio
                            WHERE Ips_Entidad = @EntidadNombre 
                               OR Ips_Entidad = @EntidadId;");

                transaction.Commit();
                return (eliminadosPadron, eliminadosConsumoCab, eliminadosConsumoDet);
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
                throw new InvalidOperationException("No se encontro la cadena de conexion en base de data");
            }

            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
