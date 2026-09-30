namespace Kairion.Application.Abstractions;

/// <summary>
/// Unit-of-work abstraction used by the application services to persist changes and
/// publish domain events. EF Core's <c>DbContext</c> satisfies this in the infrastructure
/// layer.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
