# Implementador CUAD

Aplicación de escritorio WPF (.NET 8) para importar y validar archivos de CUAD (categorías, padrón, consumos, consumos detalle, servicios, catálogo de servicios) y cargarlos en SQL Server. La aplicación trabaja con una **base** (solo lectura) y una **base por empleador** (donde se importan los 6 archivos).

## Requisitos

- .NET SDK 8.0 o superior
- SQL Server accesible desde la máquina donde corre la aplicación
- Configuración de conexiones en `Configuration.xml` (sección `<Conexiones>`)

## Ejecución

1. Abrir la solución `ImplementadorCUAD.sln` en Visual Studio.
2. Restaurar paquetes NuGet y compilar la solución.
3. Configurar `Configuration.xml` con la base y empleadores.
4. Ejecutar el proyecto `ImplementadorCUAD` como proyecto de inicio.

## Configuración
### Conexiones a bases de datos (`Configuration.xml`)

En la sección `<Conexiones>` se definen:

- **`<ConexionBase>`**: connection string de la base (solo lectura). De ahí se obtienen entidades, categorías y catálogo.
- **`<ConexionEmpleadores>`**: servidor y autenticación comunes para construir las conexiones destino de cada empleador.
- **`<Empleador>`**: cada empleador que aparece en el desplegable, con `nombre` y `baseDatos` (nombre de la base donde se importan los 6 archivos).

También se puede indicar un connection string completo por empleador con el atributo `connectionString` en lugar de `baseDatos`.
Si no existe la sección `<Conexiones>` o no hay empleadores configurados, el desplegable de empleador quedará vacío (solo "Seleccionar") y no se podrá
implementar ni limpiar hasta configurar al menos un empleador.

### Columnas de los archivos (`Configuration.xml`)

En el mismo archivo se definen, por tipo de archivo (Categorías, Padrón, Consumos, etc.), las columnas esperadas, alias y reglas (tipo, largo, requerida).
Si una columna no viene en el archivo, se puede comentar en la configuración para que la aplicación no la exija.

## Uso de la aplicación

1. Seleccionar **Empleador** y **Entidad** en los desplegables (entidades provienen de la base).
2. Cargar los archivos (Categorías, Padrón, Consumos, Consumos detalle, Servicios, Catálogo de servicios) según corresponda.
3. Pulsar **Validar** para comprobar consistencia y que no existan datos previos para esa entidad en la base del empleador.
4. Pulsar **Implementar datos** para insertar en la base del empleador seleccionado.
5. Opcional: **Limpiar entidad** borra los datos importados de la entidad seleccionada en la base del empleador.

La versión de la aplicación se muestra en la esquina superior derecha del panel (por ejemplo, `v1.0.0`).

## Versionado

La versión se define en `ImplementadorCUAD.csproj` (`Version`, `AssemblyVersion`, `FileVersion`). Al publicar una nueva versión, actualizar ahí (por ejemplo `1.0.0` → `1.1.0`).
La UI la muestra leyendo el ensamblado; no hace falta tocar otro archivo.
