namespace SlideWorks.WebApi.Features.Templates.Views;

// POST /api/templates/{id}/update-my-decks answers how many of the caller's decks moved to the latest version.
public sealed record DecksUpdated(int Count);
