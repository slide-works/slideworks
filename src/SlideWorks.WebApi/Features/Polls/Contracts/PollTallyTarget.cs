namespace SlideWorks.WebApi.Features.Polls.Contracts;

// Where AudienceEventsRelay sends a PollTally (V5): Audience goes to audience:{deckId}, with counts only when the room sees
// them; Results goes to poll-results:{roundId}, always with counts, for guests who have answered an "afterAnswer" poll.
public enum PollTallyTarget
{
    Audience,
    Results,
}
