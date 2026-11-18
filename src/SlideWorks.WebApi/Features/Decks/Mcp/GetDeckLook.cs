using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// get_deck's look (L7), in set_look's terms so an agent can pass it back: the template and palette names, the logo's
// image id (or null), the draft flag and the deck's own chrome overrides (a null field is the template's default).
// TemplateVersion is the version the deck is pinned to, null on a built-in. UpdateAvailable is true only when the deck is
// on an uploaded template whose latest published version is newer than that pin; it is false on a built-in and for a
// template that cannot be resolved.
public sealed record GetDeckLook(string Template, string Palette, string? Logo, bool Draft, ChromeOverridesView Chrome, int? TemplateVersion, bool UpdateAvailable)
{
    // template is the one ITemplates resolved for this deck.
    public static GetDeckLook From(Deck deck, ResolvedTemplate template)
        => new(
            deck.Template,
            deck.Palette,
            deck.LogoImageId,
            deck.Draft,
            ChromeOverridesView.From(deck.ChromeJson),
            deck.TemplateVersion,
            IsBehind(deck, template));

    private static bool IsBehind(Deck deck, ResolvedTemplate template)
        => !template.BuiltIn
            && string.Equals(template.Ref, deck.Template, StringComparison.Ordinal)
            && deck.TemplateVersion is { } pinned
            && template.LatestVersion is { } latest
            && latest > pinned;
}
