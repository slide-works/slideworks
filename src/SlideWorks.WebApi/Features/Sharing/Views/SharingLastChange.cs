using System.Text.Json;
using SlideWorks.WebApi.Features.Activity;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// The latest sharing change, under the dialog. ActorKind is "agent" or "person", the wire name of ActorKind, which the
// activity log stores by its C# name. AtLabel is formatted on the server ("just now").
public sealed record SharingLastChange(string Actor, string ActorKind, string AtLabel)
{
    public static SharingLastChange From(ActivityEntryRow entry, string atLabel) =>
        new(entry.ActorName, JsonNamingPolicy.CamelCase.ConvertName(entry.ActorKind), atLabel);
}
