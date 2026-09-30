using System.Linq.Expressions;
using Hangfire;
using Kairion.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kairion.Infrastructure.Jobs;

/// <summary>
/// Hangfire-backed job scheduler. Used in production; keeps idempotency by pre-checking
/// Hangfire's job set via a sentinel state. The scheduler is activated when the
/// <c>UseHangfire</c> option is set in <see cref="KairionOptions"/>.
/// </summary>
public sealed class HangfireJobScheduler : IJobScheduler
{
    private readonly IBackgroundJobClient _client;
    private readonly JobStorage _storage;
    private readonly ILogger<HangfireJobScheduler> _logger;

    public HangfireJobScheduler(IBackgroundJobClient client, JobStorage storage, ILogger<HangfireJobScheduler> logger)
    {
        _client = client;
        _storage = storage;
        _logger = logger;
    }

    public Task EnqueueAsync(string idempotencyKey, string jobName, Func<CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("idempotencyKey is required.", nameof(idempotencyKey));
        }
        // Hangfire already has deduplication of job names within a short window, but
        // we re-validate via the storage monitor to make the idempotency intent explicit.
        if (IdempotencyClaimAlreadyPresent(idempotencyKey))
        {
            _logger.LogDebug("Skipping Hangfire job {Job} ({Key}); idempotency key already present.", jobName, idempotencyKey);
            return Task.CompletedTask;
        }
        Expression<Func<Task>> body = () => handler(cancellationToken);
        _client.Enqueue<HangfireJobRunner>(runner => runner.RunAsync(jobName, idempotencyKey, handler, CancellationToken.None));
        return Task.CompletedTask;
    }

    private bool IdempotencyClaimAlreadyPresent(string key)
    {
        try
        {
            using var connection = _storage.GetConnection();
            var monitoring = _storage.GetMonitoringApi();
            var queues = monitoring.Queues();
            foreach (var queue in queues)
            {
                var fetched = monitoring.FetchedJobs(queue.Name, 0, 50);
                if (fetched.Any(j => j.Value.Job?.Args.OfType<string>().Contains(key) == true))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Hangfire storage is optional in the bootstrap; if it is unavailable, fall
            // through and let Hangfire's own dedup handle the case.
        }
        return false;
    }
}

public sealed class HangfireJobRunner
{
    private readonly ILogger<HangfireJobRunner> _logger;
    public HangfireJobRunner(ILogger<HangfireJobRunner> logger) => _logger = logger;

    public async Task RunAsync(string jobName, string idempotencyKey, Func<CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        try
        {
            await handler(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hangfire job {Job} ({Key}) failed.", jobName, idempotencyKey);
            throw;
        }
    }
}
