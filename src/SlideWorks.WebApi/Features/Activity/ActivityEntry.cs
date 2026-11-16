using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Activity;

// One change to a deck, as the change log shows it (K7, D7): who, how they came in, which MCP tool (for an agent) and
// what changed, such as "added slide 8 (practice)". The time comes from TimeProvider when it is recorded.
public sealed record ActivityEntry(string ActorName, ActorKind ActorKind, ActorVia Via, string? Tool, string Summary)
{
    // The current caller as the actor. tool is the MCP tool's name for an agent, and null for a change from the web.
    public static ActivityEntry By(ICurrentActor actor, string? tool, string summary)
        => new(actor.Name ?? "", actor.Kind, actor.Via, tool, summary);
}
