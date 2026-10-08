namespace Orders.Domain.Exceptions;

public sealed class DomainValidationException(string field, string message) : DomainException(message)
{
    public string Field { get; } = field;
}
