using System.Reflection;

namespace ImplementadorCUAD
{
    /// <summary>
    /// Versión de la aplicación para la UI. Se lee del ensamblado (definido en .csproj).
    /// </summary>
    public static class VersionInfo
    {
        public static string Version =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        public static string DisplayVersion => "v" + Version;
    }
}
