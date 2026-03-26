namespace ImplementadorCUAD.Services.Validation;

public sealed class DbValidationException : Exception
{
    public DbValidationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
