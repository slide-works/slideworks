namespace SlideWorks.WebApi.Features.Decks.Views;

// A deck someone else shared with the caller by name: a DeckRow plus the owner's name and the caller's access, "read" or "write" (C4).
public sealed record SharedDeckRow(
    string Id,
    string Slug,
    string Title,
    string? Subtitle,
    int SlideCount,
    LastChange? LastChange,
    DeckSharing Sharing,
    int ViewerCount,
    string UpdatedLabel,
    string PlayUrl,
    string OwnerName,
    string Access)
{
    public static SharedDeckRow From(DeckRow row, string ownerName, string access) => new(
        row.Id,
        row.Slug,
        row.Title,
        row.Subtitle,
        row.SlideCount,
        row.LastChange,
        row.Sharing,
        row.ViewerCount,
        row.UpdatedLabel,
        row.PlayUrl,
        ownerName,
        access);
}
