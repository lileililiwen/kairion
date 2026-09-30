using Kairion.Application.Abstractions;
using Kairion.Application.Dtos;

namespace Kairion.Application.Trends;

/// <summary>
/// Computes 7/30/90-day trend counts and labels from persisted observations. The
/// computation is deterministic: every result is reproducible from the persisted UTC
/// timestamps and the configured thresholds. AI MUST NOT supply aggregate values; the
/// service only consumes persisted state.
/// </summary>
public sealed class TrendService
{
    private readonly IObservationReadService _observations;
    private readonly IClock _clock;
    private readonly TrendThresholds _thresholds;

    public TrendService(IObservationReadService observations, IClock clock, TrendThresholds? thresholds = null)
    {
        _observations = observations;
        _clock = clock;
        _thresholds = thresholds ?? TrendThresholds.Default;
    }

    public async Task<TrendResponse> ComputeProjectTrendAsync(
        Guid projectId,
        TrendWindow window,
        CancellationToken cancellationToken)
    {
        var asOf = RoundToUtc(_clock.UtcNow);
        var (currentStart, currentEnd) = ComputeWindow(asOf, window);
        var (previousStart, previousEnd) = ComputePreviousWindow(asOf, window);

        var currentIds = await _observations
            .DistinctSourceIdsInWindowAsync(projectId, currentStart, currentEnd, clusterId: null, cancellationToken)
            .ConfigureAwait(false);
        var previousIds = await _observations
            .DistinctSourceIdsInWindowAsync(projectId, previousStart, previousEnd, clusterId: null, cancellationToken)
            .ConfigureAwait(false);

        var currentCount = currentIds.Count;
        var previousCount = previousIds.Count;
        var (percentGrowth, newSignal) = ComputeGrowth(currentCount, previousCount);

        return new TrendResponse
        {
            ProjectId = projectId,
            Window = window.Label(),
            AsOfUtc = asOf,
            WindowStartUtc = currentStart,
            WindowEndUtc = currentEnd,
            CurrentCount = currentCount,
            PreviousCount = previousCount,
            PercentGrowth = percentGrowth,
            NewSignal = newSignal,
            Label = LabelFor(percentGrowth, newSignal),
        };
    }

    public ClusterTrend ComputeClusterTrend(
        Guid clusterId,
        string label,
        int currentCount,
        int previousCount)
    {
        var (percentGrowth, newSignal) = ComputeGrowth(currentCount, previousCount);
        return new ClusterTrend
        {
            ClusterId = clusterId,
            Label = label,
            CurrentCount = currentCount,
            PreviousCount = previousCount,
            PercentGrowth = percentGrowth,
            NewSignal = newSignal,
            LabelTrend = LabelFor(percentGrowth, newSignal),
        };
    }

    public (decimal? percentGrowth, bool newSignal) ComputeGrowth(int currentCount, int previousCount)
    {
        if (previousCount == 0)
        {
            // Never divide by zero. When the prior window has no observations, growth is
            // undefined and the response must mark the signal as new.
            return (percentGrowth: null, newSignal: currentCount > 0);
        }
        var raw = (decimal)(currentCount - previousCount) / previousCount;
        return (percentGrowth: Math.Round(raw, 4, MidpointRounding.ToEven), newSignal: false);
    }

    public string LabelFor(decimal? percentGrowth, bool newSignal)
    {
        if (newSignal) return "new_signal";
        if (percentGrowth is null) return "no_baseline";
        if (percentGrowth >= _thresholds.EmergingGrowth) return "emerging";
        if (percentGrowth <= _thresholds.DecliningGrowth) return "declining";
        return "stable";
    }

    public (DateTime windowStartUtc, DateTime windowEndUtc) ComputeWindow(DateTime asOfUtc, TrendWindow window)
    {
        var end = asOfUtc;
        var start = asOfUtc.AddDays(-(int)window);
        return (start, end);
    }

    public (DateTime windowStartUtc, DateTime windowEndUtc) ComputePreviousWindow(DateTime asOfUtc, TrendWindow window)
    {
        var (currentStart, _) = ComputeWindow(asOfUtc, window);
        var end = currentStart;
        var start = currentStart.AddDays(-(int)window);
        return (start, end);
    }

    private static DateTime RoundToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}
