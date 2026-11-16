using System.Text.Json;
using SlideWorks.WebApi.Features.Activity;

namespace SlideWorks.WebApi.Features.Decks.Views;

// A library row's latest change. ActorKind is "agent" or "person" and Via is "mcp" or "web": the wire names of
// ActorKind and ActorVia, which the activity log stores by their C# names.
public sealed record LastChange(string Actor, string ActorKind, string Via, string Summary)
{
    public static LastChange From(ActivityEntryRow entry) => new(
        entry.ActorName,
        JsonNamingPolicy.CamelCase.ConvertName(entry.ActorKind),
        JsonNamingPolicy.CamelCase.ConvertName(entry.Via),
        entry.Summary);
}
