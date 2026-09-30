using System.Collections.Concurrent;
using Kairion.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kairion.Infrastructure.Jobs;

/// <summary>
/// Synchronous in-process job runner. Used when Hangfire is disabled (tests, demos) and
/// as a fallback so the MVP runs without a separate scheduler. The implementation
/// honors idempotency: re-enqueuing the same key while the previous job is in flight
/// or recently completed is a no-op.
/// </summary>
public sealed class InProcessJobScheduler : IJobScheduler
{
    private static readonly TimeSpan RecentlyCompletedTtl = TimeSpan.FromMinutes(5);
    private readonly ILogger<InProcessJobScheduler> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _completed = new();
    private readonly ConcurrentDictionary<string, byte> _inFlight = new();

    public InProcessJobScheduler(ILogger<InProcessJobScheduler> logger) => _logger = logger;

    public async Task EnqueueAsync(string idempotencyKey, string jobName, Func<CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("idempotencyKey is required.", nameof(idempotencyKey));
        }
        if (!_inFlight.TryAdd(idempotencyKey, 0))
        {
            _logger.LogDebug("Skipping duplicate job {Job} ({Key}); already in flight.", jobName, idempotencyKey);
            return;
        }
        if (_completed.TryGetValue(idempotencyKey, out var finishedAt) && DateTime.UtcNow - finishedAt < RecentlyCompletedTtl)
        {
            _logger.LogDebug("Skipping duplicate job {Job} ({Key}); completed at {Finished}.", jobName, idempotencyKey, finishedAt);
            _inFlight.TryRemove(idempotencyKey, out _);
            return;
        }
        try
        {
            await handler(cancellationToken).ConfigureAwait(false);
            _completed[idempotencyKey] = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is propagated; the key remains in-flight so a retry sees the cancel.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "In-process job {Job} ({Key}) failed.", jobName, idempotencyKey);
        }
        finally
        {
            _inFlight.TryRemove(idempotencyKey, out _);
        }
    }
}
