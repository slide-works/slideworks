using Agoda.IoC.Core;
using SlideWorks.Core.Join;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Join.Views;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Live;

// Builds the join object (J1) the play view, the deck page and the render page carry. The links come from IJoinUrls and
// live from the deck's open session, so no view reads the request's host.
[RegisterPerRequest]
public class JoinViews(IJoinUrls joinUrls, IAudienceSessions sessions)
{
    public async Task<JoinView> ForAsync(Deck deck)
    {
        var found = await sessions.ForDeckAsync(deck.Id);
        return new JoinView(deck.JoinCode, JoinCodes.Display(deck.JoinCode), joinUrls.JoinUrl(deck.JoinCode), joinUrls.HostLabel(), found?.Phase == AudiencePhase.Live);
    }
}
