namespace SlideWorks.WebApi.Features.Checks;

// Errors and warnings are for the agent to fix; a note asks it to look. The order is the sort order.
public enum FindingSeverity
{
    Error,
    Warning,
    Note,
}
