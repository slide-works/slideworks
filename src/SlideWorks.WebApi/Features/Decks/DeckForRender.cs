using SlideWorks.WebApi.Features.Decks.Views;

namespace SlideWorks.WebApi.Features.Decks;

// DeckReader.ReadForRenderAsync: the play view plus what the share card and its cache need (L9). Read with no access
// check, so only the render API (behind a render token) and the card cache use it.
public sealed record DeckForRender(DeckPlayView Play, string? Subtitle, long Revision, DeckSharing Sharing);
