namespace Kairion.Application.Abstractions;

/// <summary>
/// Schedules background work. The concrete implementation in infrastructure uses Hangfire;
/// tests substitute a synchronous runner that returns immediately.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Enqueue a job by idempotency key. Re-enqueuing the same key MUST NOT cause
    /// a duplicate execution when the previous one is still pending or in flight.</summary>
    Task EnqueueAsync(string idempotencyKey, string jobName, Func<CancellationToken, Task> handler, CancellationToken cancellationToken);
}
