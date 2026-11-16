namespace SlideWorks.WebApi.Features.Decks.Views;

// One of the caller's decks in the library. Sharing is the level's wire name: "private", "link" or "named".
// UpdatedLabel is formatted on the server ("2 min ago"), and PlayUrl is root-relative: /d/{id}/{slug}.
public sealed record DeckRow(
    string Id,
    string Slug,
    string Title,
    string? Subtitle,
    int SlideCount,
    LastChange? LastChange,
    string Sharing,
    int ViewerCount,
    string UpdatedLabel,
    string PlayUrl);
