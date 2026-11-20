using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Templates.Views;

// One template on the templates page (templates.json). Name is the label and ShortName the manifest's name. Type is
// "Head family / Body family". Palette is the palette's label, or "any" when the template brings none. Version is 0
// while only a draft exists. Owner is a display name, and null on the caller's own template and on a built-in.
public sealed record TemplateRowView(
    string Id,
    string Name,
    string ShortName,
    string Purpose,
    string Type,
    string Palette,
    bool PaletteLocked,
    int Version,
    TemplateStatus Status,
    int DeckCount,
    string UpdatedLabel,
    DeckSharing Sharing,
    string? Owner);
