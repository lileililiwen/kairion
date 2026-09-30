namespace Kairion.Domain;

/// <summary>
/// A persisted human revision. The payload captures the inputs that produced the change
/// and the timestamp the owner recorded it. The system never silently overwrites a human
/// decision with a later model output.
/// </summary>
public sealed class HumanRevision
{
    public HumanRevision(
        Guid id,
        HumanRevisionAction action,
        Guid? clusterId,
        Guid? sourceItemId,
        string payloadJson,
        DateTime createdUtc)
    {
        if (id == Guid.Empty) throw new DomainValidationException("HumanRevision.Id is required.");
        Id = id;
        Action = action;
        ClusterId = clusterId;
        SourceItemId = sourceItemId;
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        CreatedUtc = EnsureUtc(createdUtc);
    }

    public Guid Id { get; }
    public HumanRevisionAction Action { get; }
    public Guid? ClusterId { get; }
    public Guid? SourceItemId { get; }
    public string PayloadJson { get; }
    public DateTime CreatedUtc { get; }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
