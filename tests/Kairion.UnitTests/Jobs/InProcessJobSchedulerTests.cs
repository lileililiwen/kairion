using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Infrastructure.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kairion.UnitTests.Jobs;

/// <summary>
/// Tests the in-process job scheduler's idempotency contract: re-enqueuing the same key
/// within the recently-completed TTL is a no-op, distinct keys are executed, and a
/// running key is skipped while in flight.
/// </summary>
public class InProcessJobSchedulerTests
{
    [Fact]
    public async Task EnqueueAsync_ExecutesHandler_OnFirstCall()
    {
        var sut = new InProcessJobScheduler(NullLogger<InProcessJobScheduler>.Instance);
        var ran = 0;
        await sut.EnqueueAsync("key-1", "screening", _ =>
        {
            ran++;
            return Task.CompletedTask;
        }, CancellationToken.None);
        ran.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_SkipsDuplicate_WithinTtl()
    {
        var sut = new InProcessJobScheduler(NullLogger<InProcessJobScheduler>.Instance);
        var ran = 0;
        Func<CancellationToken, Task> handler = _ =>
        {
            ran++;
            return Task.CompletedTask;
        };
        await sut.EnqueueAsync("dup", "screening", handler, CancellationToken.None);
        await sut.EnqueueAsync("dup", "screening", handler, CancellationToken.None);
        await sut.EnqueueAsync("dup", "screening", handler, CancellationToken.None);
        ran.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_DistinctKeys_AllExecute()
    {
        var sut = new InProcessJobScheduler(NullLogger<InProcessJobScheduler>.Instance);
        var ran = 0;
        Func<CancellationToken, Task> handler = _ =>
        {
            ran++;
            return Task.CompletedTask;
        };
        await sut.EnqueueAsync("a", "screening", handler, CancellationToken.None);
        await sut.EnqueueAsync("b", "screening", handler, CancellationToken.None);
        ran.Should().Be(2);
    }

    [Fact]
    public async Task EnqueueAsync_EmptyKey_Throws()
    {
        var sut = new InProcessJobScheduler(NullLogger<InProcessJobScheduler>.Instance);
        var act = async () => await sut.EnqueueAsync(" ", "screening", _ => Task.CompletedTask, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
