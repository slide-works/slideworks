namespace SlideWorks.WebApi.Features.Look.Views;

// A deck's resolved look (L6), in the play view, the deck page, get_deck's render and the card view. FooterDateLabel is
// the deck's last change as a server-formatted day.
public sealed record LookView(LookTemplateView Template, LookPaletteView Palette, LookLogoView? Logo, bool Draft, string FooterDateLabel);
