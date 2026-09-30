using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kairion.Application.Abstractions;
using Kairion.Application.Trends;
using NSubstitute;
using Xunit;

namespace Kairion.UnitTests.Trends;

/// <summary>
/// Deterministic tests for the trend math. The service must never divide by zero and
/// must always return a stable label that downstream code (UI, opportunity signals) can
/// trust.
/// </summary>
public class TrendServiceTests
{
    private static readonly DateTime AsOf = new(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ComputeProjectTrendAsync_NonZeroBaseline_ReturnsRoundedPercentAndEmergingLabel()
    {
        var observations = Substitute.For<IObservationReadService>();
        var currentWindowStart = AsOf.AddDays(-30);
        var previousWindowStart = AsOf.AddDays(-60);
        var previousWindowEnd = currentWindowStart;

        observations
            .DistinctSourceIdsInWindowAsync(Arg.Any<Guid>(), currentWindowStart, AsOf, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(MakeIds(6));
        observations
            .DistinctSourceIdsInWindowAsync(Arg.Any<Guid>(), previousWindowStart, previousWindowEnd, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(MakeIds(4));

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(AsOf);

        var sut = new TrendService(observations, clock);
        var response = await sut.ComputeProjectTrendAsync(Guid.NewGuid(), TrendWindow.Days30, CancellationToken.None);

        response.CurrentCount.Should().Be(6);
        response.PreviousCount.Should().Be(4);
        response.PercentGrowth.Should().Be(0.5m);
        response.NewSignal.Should().BeFalse();
        response.Label.Should().Be("emerging");
    }

    [Fact]
    public async Task ComputeProjectTrendAsync_ZeroPrevious_ReturnsNullPercentAndNewSignalTrue()
    {
        var observations = Substitute.For<IObservationReadService>();
        var currentWindowStart = AsOf.AddDays(-30);
        var previousWindowStart = AsOf.AddDays(-60);
        var previousWindowEnd = currentWindowStart;

        observations
            .DistinctSourceIdsInWindowAsync(Arg.Any<Guid>(), currentWindowStart, AsOf, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(MakeIds(3));
        observations
            .DistinctSourceIdsInWindowAsync(Arg.Any<Guid>(), previousWindowStart, previousWindowEnd, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>());

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(AsOf);

        var sut = new TrendService(observations, clock);
        var response = await sut.ComputeProjectTrendAsync(Guid.NewGuid(), TrendWindow.Days30, CancellationToken.None);

        response.PercentGrowth.Should().BeNull();
        response.NewSignal.Should().BeTrue();
        response.Label.Should().Be("new_signal");
    }

    [Fact]
    public async Task ComputeProjectTrendAsync_BothWindowsEmpty_ReturnsNoBaseline()
    {
        var observations = Substitute.For<IObservationReadService>();
        observations
            .DistinctSourceIdsInWindowAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>());

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(AsOf);

        var sut = new TrendService(observations, clock);
        var response = await sut.ComputeProjectTrendAsync(Guid.NewGuid(), TrendWindow.Days30, CancellationToken.None);

        response.CurrentCount.Should().Be(0);
        response.PreviousCount.Should().Be(0);
        response.PercentGrowth.Should().BeNull();
        response.NewSignal.Should().BeFalse();
        response.Label.Should().Be("no_baseline");
    }

    [Theory]
    [InlineData(10, 1, 9.0, "emerging")]
    [InlineData(1, 10, -0.9, "declining")]
    [InlineData(5, 5, 0.0, "stable")]
    public async Task LabelFor_PercentChanges_UseThresholds(int current, int previous, double expectedGrowth, string expectedLabel)
    {
        var observations = Substitute.For<IObservationReadService>();
        var currentWindowStart = AsOf.AddDays(-30);
        var previousWindowStart = AsOf.AddDays(-60);
        var previousWindowEnd = currentWindowStart;

        observations
            .DistinctSourceIdsInWindowAsync(Arg.Any<Guid>(), currentWindowStart, AsOf, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(MakeIds(current));
        observations
            .DistinctSourceIdsInWindowAsync(Arg.Any<Guid>(), previousWindowStart, previousWindowEnd, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(MakeIds(previous));

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(AsOf);

        var sut = new TrendService(observations, clock);
        var response = await sut.ComputeProjectTrendAsync(Guid.NewGuid(), TrendWindow.Days30, CancellationToken.None);

        response.PercentGrowth.Should().Be((decimal)expectedGrowth);
        response.Label.Should().Be(expectedLabel);
    }

    [Fact]
    public void ComputeWindow_HasRequestedLength()
    {
        var sut = new TrendService(Substitute.For<IObservationReadService>(), Substitute.For<IClock>());
        var (start, end) = sut.ComputeWindow(AsOf, TrendWindow.Days7);
        (end - start).TotalDays.Should().Be(7);
    }

    [Fact]
    public void ComputePreviousWindow_IsContiguousBeforeCurrent()
    {
        var sut = new TrendService(Substitute.For<IObservationReadService>(), Substitute.For<IClock>());
        var (currentStart, _) = sut.ComputeWindow(AsOf, TrendWindow.Days30);
        var (prevStart, prevEnd) = sut.ComputePreviousWindow(AsOf, TrendWindow.Days30);
        prevEnd.Should().Be(currentStart);
        (currentStart - prevStart).TotalDays.Should().Be(30);
    }

    private static IReadOnlyList<Guid> MakeIds(int n)
    {
        var ids = new Guid[n];
        for (var i = 0; i < n; i++) ids[i] = Guid.NewGuid();
        return ids;
    }
}
