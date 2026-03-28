namespace Implementador.Application.Validation.Common;

/// <summary>
/// Nombres canónicos de archivos para mensajes de log.
/// Centraliza la traducción de clave lógica (usada en XML) a nombre visible.
/// </summary>
public static class ArchivoNombre
{
    public const string CategoriasSOCIOS  = "Categorias Socios";
    public const string PadronSocios      = "Padron Socios";
    public const string Consumos          = "Consumos";
    public const string ConsumosDetalle   = "Consumos Detalle";
    public const string CatalogoServicios = "Catalogo Servicios";
    public const string ConsumosServicios = "Consumos Servicios";

    /// <summary>Traduce la clave lógica (del XML) al nombre de display para logs.</summary>
    public static string FromKey(string key) => key switch
    {
        "Categorias"      => CategoriasSOCIOS,
        "Padron"          => PadronSocios,
        "Consumos"        => Consumos,
        "ConsumosDetalle" => ConsumosDetalle,
        "CatalogoServicios" => CatalogoServicios,
        "Servicios"       => ConsumosServicios,
        _                 => key
    };
}

/// <summary>
/// Plantilla de mensajes de log para validación de archivos.
/// Plantilla por fila  : "{Archivo} fila {N}: {detalle}"
/// Plantilla resumen   : "{Archivo}: {mensaje}"
/// </summary>
public static class ValidationLog
{
    // ── Por fila ──────────────────────────────────────────────────────────
    public static string FilaError(string archivo, int fila, string detalle)
        => $"{archivo} fila {fila}: {detalle}";

    // ── Carga / formato ───────────────────────────────────────────────────
    public static string ArchivoVacio(string archivo)
        => $"{archivo}: el archivo se encuentra vacio. No se cargaron registros.";

    public static string FormatoRechazadas(string archivo, int rechazadas, int total)
        => $"{archivo}: {rechazadas}/{total} filas rechazadas por formato o tipo de dato invalido.";

    // ── Resultado final de validación de negocio ──────────────────────────
    public static string ListasParaImplementar(string archivo, int listas)
        => $"{archivo}: {listas} filas listas para implementar.";

    public static string ListasParaImplementarConRechazadas(string archivo, int listas, int rechazadas)
        => $"{archivo}: {listas} filas listas para implementar ({rechazadas} rechazadas por reglas de negocio).";
}
