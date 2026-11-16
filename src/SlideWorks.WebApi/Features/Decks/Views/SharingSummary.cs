namespace SlideWorks.WebApi.Features.Decks.Views;

// The deck page's sharing line. Level goes on the wire as "private", "link" or "named" (every enum is camelCase on the
// wire).
public sealed record SharingSummary(DeckSharing Level, int ViewerCount)
{
    public static SharingSummary From(DeckSharing level, int viewerCount) => new(level, viewerCount);
}
