using Agoda.IoC.Core;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Qa;

// TODO: replace with the real room: the deck's settings, the switch of the presenter's slide, the open Q&A round and its revision.
// Null unless the deck is live; while live, the room for the default settings with no round, since no Q&A round is opened yet.
[RegisterPerRequest(For = typeof(IAudienceRoom))]
public sealed class StubAudienceRoom(IAudienceSessions sessions) : IAudienceRoom
{
    public async Task<QaRoom?> ForDeckAsync(string deckId)
        => await sessions.ForDeckAsync(deckId) is { Phase: AudiencePhase.Live }
            ? QaRoomRules.For(QaSettings.Default, QaSlideSwitch.On, new QaRoundState(null, 0))
            : null;
}
