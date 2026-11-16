using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Decks.Views;

// A library row's latest change. ActorKind goes on the wire as "agent" or "person" and Via as "mcp" or "web" (every
// enum is camelCase on the wire); the activity log stores both by their C# names.
public sealed record LastChange(string Actor, ActorKind ActorKind, ActorVia Via, string Summary)
{
    public static LastChange From(ActivityEntryRow entry) => new(
        entry.ActorName,
        Enum.Parse<ActorKind>(entry.ActorKind),
        Enum.Parse<ActorVia>(entry.Via),
        entry.Summary);
}
