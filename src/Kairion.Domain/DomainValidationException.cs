namespace Kairion.Domain;

/// <summary>
/// Thrown when domain invariants or input validation rules are violated. Mapped to
/// HTTP 400 in the API layer.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
    public DomainValidationException(string message, Exception inner) : base(message, inner) { }
}
