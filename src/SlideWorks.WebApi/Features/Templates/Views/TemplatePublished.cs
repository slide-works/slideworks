namespace SlideWorks.WebApi.Features.Templates.Views;

// POST /api/templates/{id}/publish: the new version, and how many of the caller's own decks are on an older one.
public sealed record TemplatePublished(int Version, int DecksBehind);
