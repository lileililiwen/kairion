using Kairion.Application.Abstractions;

namespace Kairion.Application.Trends;

/// <summary>
/// A named, versioned trend window. The 7/30/90-day windows are the only ones supported
/// in the MVP; introducing a new window is a versioned configuration change.
/// </summary>
public enum TrendWindow
{
    Days7 = 7,
    Days30 = 30,
    Days90 = 90,
}

public static class TrendWindowExtensions
{
    public static string Label(this TrendWindow window) => window switch
    {
        TrendWindow.Days7 => "7d",
        TrendWindow.Days30 => "30d",
        TrendWindow.Days90 => "90d",
        _ => throw new ArgumentOutOfRangeException(nameof(window), window, "Unknown trend window."),
    };

    public static bool TryParse(string? text, out TrendWindow window)
    {
        switch ((text ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "7d":
                window = TrendWindow.Days7;
                return true;
            case "30d":
                window = TrendWindow.Days30;
                return true;
            case "90d":
                window = TrendWindow.Days90;
                return true;
            default:
                window = default;
                return false;
        }
    }
}

/// <summary>
/// Deterministic thresholds for the Emerging/Stable/Declining label. Stored as
/// configuration so the version can be changed without a code change; the default values
/// match the design baseline.
/// </summary>
public sealed class TrendThresholds
{
    public TrendThresholds(decimal emergingGrowth, decimal decliningGrowth)
    {
        if (emergingGrowth <= 0m) throw new ArgumentException("emergingGrowth must be > 0.", nameof(emergingGrowth));
        if (decliningGrowth >= 0m) throw new ArgumentException("decliningGrowth must be < 0.", nameof(decliningGrowth));
        EmergingGrowth = emergingGrowth;
        DecliningGrowth = decliningGrowth;
    }

    /// <summary>Default thresholds documented in the design baseline.</summary>
    public static readonly TrendThresholds Default = new(emergingGrowth: 0.5m, decliningGrowth: -0.5m);

    public decimal EmergingGrowth { get; }
    public decimal DecliningGrowth { get; }
}
