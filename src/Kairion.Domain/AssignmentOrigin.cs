namespace Kairion.Domain;

/// <summary>
/// The provenance of a cluster assignment. Human assignments take precedence over later
/// AI suggestions until the owner explicitly changes them.
/// </summary>
public enum AssignmentOrigin
{
    Ai = 0,
    Human = 1,
}
