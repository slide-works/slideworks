namespace SlideWorks.WebApi.Features.Decks.Mcp;

// create_deck's look (L7): the template (a built-in's name or an uploaded template's tpl_… id) and palette the new deck
// got, and the template version it is pinned to: the latest published one, or null on a built-in.
public sealed record CreateDeckLook(string Template, string Palette, int? TemplateVersion);
