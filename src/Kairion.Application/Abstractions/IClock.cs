namespace Kairion.Application.Abstractions;

/// <summary>
/// Wall-clock abstraction. Allows tests and deterministic services to pin time without
/// depending on <see cref="DateTime.UtcNow"/> directly.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
