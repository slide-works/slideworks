namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// Where AudienceEventsRelay sends a WordCloudTally or an OpenTextTally (SW-8): Audience goes to audience:{deckId} with the
// hidden entries left out, Presenter goes to audience-watch:{deckId} with every entry and its hidden flag.
public enum OpenResponseTarget
{
    Audience,
    Presenter,
}
