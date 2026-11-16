using System.Text.Json;

namespace SlideWorks.WebApi.Features.Decks.Views;

// The deck page's sharing line. Level is the wire name of DeckSharing: "private", "link" or "named".
public sealed record SharingSummary(string Level, int ViewerCount)
{
    public static SharingSummary From(DeckSharing level, int viewerCount) =>
        new(JsonNamingPolicy.CamelCase.ConvertName(level.ToString()), viewerCount);
}
