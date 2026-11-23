using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Decks.Views;

// One entry of the deck page's change log. ActorKind goes on the wire as "agent" or "person" and Via as "mcp" or "web"
// (every enum is camelCase on the wire); the activity log stores both by their C# names. Tool is the MCP tool, null for
// a change made in the browser. AtLabel is formatted on the server ("2 min ago"). IsMe is true for a person's entry made by the viewer (the entry's actor user id is the viewer's id).
public sealed record ActivityRow(string Actor, ActorKind ActorKind, ActorVia Via, string? Tool, string Summary, string AtLabel, bool IsMe)
{
    public static ActivityRow From(ActivityEntryRow entry, string atLabel, bool isOwner, Guid? viewerId) => new(
        entry.ActorName,
        Enum.Parse<ActorKind>(entry.ActorKind),
        Enum.Parse<ActorVia>(entry.Via),
        entry.Tool,
        SharingActivity.Visible(entry.Summary, isOwner),
        atLabel,
        entry.ActorKind == nameof(ActorKind.Person) && entry.ActorUserId is not null && entry.ActorUserId == viewerId);
}
