using System.Net;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.RateLimits;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Join;

// Builds the guest view for a join code (J3). IAudienceSessions decides whether the code means anything and which phase
// it is in, and IAudienceRelay supplies the position; nothing here computes a phase. A code with no session is 404
// "no_session", the same body whatever the reason. Only misses count toward the rate limit (S2): a hit is never limited, and
// once the caller's misses are used up a further miss is 429 "too_many_attempts".
[RegisterPerRequest]
public class GuestViews(
    AppDbContext db,
    IAudienceSessions sessions,
    IAudienceRelay relay,
    IJoinLookupLimiter limiter,
    ITemplateCatalogue templates,
    IPaletteCatalogue palettes)
{
    public const string NO_SESSION = "no_session";
    public const string TOO_MANY_ATTEMPTS = "too_many_attempts";

    public async Task<GuestView> GetAsync(string code, IPAddress? caller)
    {
        var found = await sessions.FindAsync(code);
        if (found is null)
        {
            var key = CallerKey.For(caller);
            if (!limiter.Allow(key))
            {
                throw new TooManyRequestsException(TOO_MANY_ATTEMPTS);
            }
            limiter.Miss(key);
            throw new NotFoundException(NO_SESSION);
        }

        var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == found.DeckId);
        var presenter = await db.Users.AsNoTracking().Where(u => u.Id == deck.OwnerId).Select(u => u.Name).SingleAsync();
        // The relay answers null only for a deck with no session, and the sessions just found one: that is a fault, not a
        // lobby to make up.
        var state = await relay.CurrentAsync(found.DeckId)
            ?? throw new InvalidOperationException($"The relay has no state for deck {found.DeckId}, which has a session.");
        return new GuestView(
            deck.Title,
            presenter,
            LookViews.From(deck, templates, palettes),
            deck.Sharing == DeckSharing.Link ? DeckLinks.Play(deck.Id, deck.Slug) : null,
            state);
    }
}
