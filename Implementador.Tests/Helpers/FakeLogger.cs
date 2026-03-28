using Implementador.Infrastructure;

namespace Implementador.Tests.Helpers;

/// <summary>
/// Logger falso para tests: acumula mensajes para poder verificarlos.
/// </summary>
public sealed class FakeLogger : IAppLogger
{
    public List<string> InfoMessages { get; } = [];
    public List<string> WarnMessages { get; } = [];
    public List<string> ErrorMessages { get; } = [];

    public void Info(string message) => InfoMessages.Add(message);
    public void Warn(string message) => WarnMessages.Add(message);
    public void Error(string message) => ErrorMessages.Add(message);
    public void Error(Exception ex, string message) => ErrorMessages.Add($"{message}: {ex.Message}");

    public bool HasErrors => ErrorMessages.Count > 0;
    public bool HasWarnings => WarnMessages.Count > 0;
}
