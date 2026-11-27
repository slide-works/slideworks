namespace SlideWorks.WebApi.Features.Decks;

// A deck's name after set_title: its title, subtitle, author and the slug its address now uses.
public sealed record DeckTitle(string Title, string? Subtitle, string? Author, string Slug);
