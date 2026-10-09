namespace Orders.Domain.Exceptions;

/// <summary>Erro de regra de negócio. A API traduz para respostas 4xx.</summary>
public abstract class DomainException(string message) : Exception(message);
