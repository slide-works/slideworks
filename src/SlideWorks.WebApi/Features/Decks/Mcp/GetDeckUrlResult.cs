namespace SlideWorks.WebApi.Features.Decks.Mcp;

// get_deck_url: { url }, the absolute player link: {public}/d/{id}/{slug}, plus #{slide} or #{slide}.{step}.
public sealed record GetDeckUrlResult(string Url);
