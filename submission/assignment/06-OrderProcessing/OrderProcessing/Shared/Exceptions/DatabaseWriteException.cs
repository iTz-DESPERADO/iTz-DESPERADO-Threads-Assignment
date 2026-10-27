namespace OrderProcessing.Shared.Exceptions;

public sealed class DatabaseWriteException : Exception
{
    public DatabaseWriteException(string message) : base(message) { }
}
