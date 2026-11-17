using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa;

// What the room may do on the slide on screen (Q1, Q3): QaRoomRules.For over the deck's settings, the switch of the
// presenter's slide, the open Q&A round and QaRevisions.Current. Null in every phase but live. Read-only: it never
// publishes. AudienceFeed resolves it in the scope it already opens. Per request, because it uses the database.
public interface IAudienceRoom
{
    Task<QaRoom?> ForDeckAsync(string deckId);
}
