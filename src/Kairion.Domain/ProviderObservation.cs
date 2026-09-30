namespace Kairion.Domain;

/// <summary>
/// A single provider observation. Attached to a source item or to a provider attempt so
/// that every read or import can be inspected after the fact without exposing secrets.
/// This is a value object: instances are immutable and the fields are stored as
/// first-class columns on the owning <see cref="SourceItem"/>; the instance is
/// recomputed from those columns on each access.
/// </summary>
public sealed record ProviderObservation
{
    public string ProviderId { get; }
    public ProviderStatus Status { get; }
    public string? ErrorCode { get; }
    public string? Message { get; }
    public DateTime ObservedUtc { get; }
    public int? HttpStatus { get; }

    public ProviderObservation(
        string providerId,
        ProviderStatus status,
        string? errorCode,
        string? message,
        DateTime observedUtc,
        int? httpStatus = null)
    {
        ProviderId = (providerId ?? throw new ArgumentNullException(nameof(providerId))).Trim();
        Status = status;
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : Redact(message);
        ObservedUtc = observedUtc.Kind == DateTimeKind.Utc
            ? observedUtc
            : DateTime.SpecifyKind(observedUtc, DateTimeKind.Utc);
        HttpStatus = httpStatus;
    }

    private static string Redact(string message)
    {
        // Strip anything that looks like a credential or session token from the
        // message before it is persisted. The MVP keeps the redactor conservative
        // — better to lose minor context than to leak a key.
        var patterns = new[] { "Bearer ", "Basic ", "api_key=", "token=", "password=" };
        var redacted = message;
        foreach (var p in patterns)
        {
            var idx = redacted.IndexOf(p, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                redacted = redacted[..idx] + "[redacted]";
                break;
            }
        }
        return redacted;
    }
}
