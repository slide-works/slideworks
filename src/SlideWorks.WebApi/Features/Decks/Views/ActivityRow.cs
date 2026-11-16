using System.Text.Json;
using SlideWorks.WebApi.Features.Activity;

namespace SlideWorks.WebApi.Features.Decks.Views;

// One entry of the deck page's change log. ActorKind is "agent" or "person" and Via is "mcp" or "web": the wire names
// of ActorKind and ActorVia, which the activity log stores by their C# names. Tool is the MCP tool, null for a change
// made in the browser. AtLabel is formatted on the server ("2 min ago").
public sealed record ActivityRow(string Actor, string ActorKind, string Via, string? Tool, string Summary, string AtLabel)
{
    public static ActivityRow From(ActivityEntryRow entry, string atLabel) => new(
        entry.ActorName,
        JsonNamingPolicy.CamelCase.ConvertName(entry.ActorKind),
        JsonNamingPolicy.CamelCase.ConvertName(entry.Via),
        entry.Tool,
        entry.Summary,
        atLabel);
}
