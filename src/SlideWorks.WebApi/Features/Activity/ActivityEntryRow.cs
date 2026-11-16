namespace SlideWorks.WebApi.Features.Activity;

// One stored change-log entry for a deck. Written only through IActivityLog.
public class ActivityEntryRow
{
    public long Id { get; set; }

    public required string DeckId { get; set; }

    public required string ActorName { get; set; }

    // The actor's kind (person or agent) and the way in (web or MCP), stored as their names.
    public required string ActorKind { get; set; }

    public required string Via { get; set; }

    // The MCP tool that made the change, when an agent made it.
    public string? Tool { get; set; }

    public required string Summary { get; set; }

    public DateTimeOffset At { get; set; }
}
