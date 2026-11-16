using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Decks.Views;

// One entry of the deck page's change log. ActorKind goes on the wire as "agent" or "person" and Via as "mcp" or "web"
// (every enum is camelCase on the wire); the activity log stores both by their C# names. Tool is the MCP tool, null for
// a change made in the browser. AtLabel is formatted on the server ("2 min ago").
public sealed record ActivityRow(string Actor, ActorKind ActorKind, ActorVia Via, string? Tool, string Summary, string AtLabel)
{
    public static ActivityRow From(ActivityEntryRow entry, string atLabel) => new(
        entry.ActorName,
        Enum.Parse<ActorKind>(entry.ActorKind),
        Enum.Parse<ActorVia>(entry.Via),
        entry.Tool,
        entry.Summary,
        atLabel);
}
