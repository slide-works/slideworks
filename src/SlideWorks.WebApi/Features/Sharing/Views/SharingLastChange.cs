using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// The latest sharing change, under the dialog. ActorKind goes on the wire as "agent" or "person" (every enum is camelCase
// on the wire); the activity log stores it by its C# name. AtLabel is formatted on the server ("just now").
public sealed record SharingLastChange(string Actor, ActorKind ActorKind, string AtLabel)
{
    public static SharingLastChange From(ActivityEntryRow entry, string atLabel) =>
        new(entry.ActorName, Enum.Parse<ActorKind>(entry.ActorKind), atLabel);
}
