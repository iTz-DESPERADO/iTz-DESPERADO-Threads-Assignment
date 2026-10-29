namespace OrderProcessing.Shared.Exceptions;

public sealed class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException(string message) : base(message) { }
}
