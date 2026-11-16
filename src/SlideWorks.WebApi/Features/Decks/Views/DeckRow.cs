namespace SlideWorks.WebApi.Features.Decks.Views;

// One of the caller's decks in the library. Sharing goes on the wire as "private", "link" or "named" (every enum is
// camelCase on the wire). UpdatedLabel is formatted on the server ("2 min ago"), and PlayUrl is root-relative:
// /d/{id}/{slug}.
public sealed record DeckRow(
    string Id,
    string Slug,
    string Title,
    string? Subtitle,
    int SlideCount,
    LastChange? LastChange,
    DeckSharing Sharing,
    int ViewerCount,
    string UpdatedLabel,
    string PlayUrl);
