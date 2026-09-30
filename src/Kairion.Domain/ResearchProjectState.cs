namespace Kairion.Domain;

/// <summary>
/// Lifecycle state for a research project. The brief remains editable while <see cref="Active"/>
/// and can be returned to from <see cref="Archived"/>. Hard deletion is an explicit, separate
/// operation and not represented here.
/// </summary>
public enum ResearchProjectState
{
    Active = 1,
    Archived = 2,
}
