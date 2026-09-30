namespace Kairion.Domain;

/// <summary>
/// Discriminator describing the shape of a research project's brief. Stored as a string in
/// the database for forward compatibility.
/// </summary>
public enum BriefKind
{
    /// <summary>Market-level exploration.</summary>
    Market = 1,

    /// <summary>Product category focused exploration.</summary>
    Category = 2,

    /// <summary>Competitor-focused exploration.</summary>
    Competitor = 3,

    /// <summary>Free-form research question.</summary>
    Question = 4,
}
