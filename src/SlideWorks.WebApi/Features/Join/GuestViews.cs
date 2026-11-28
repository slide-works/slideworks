using System.Net;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.RateLimits;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Join;

// Builds the guest view for a join code (J3). deckUrl is absolute, from the public URL, because the guest page may be
// served from the join host (D21). The look's logo is served to a guest only while live (D1), so a lobby or ended screen must
// not render it. IAudienceSessions decides whether the code means anything and which phase
// it is in, and IAudienceRelay supplies the position; nothing here computes a phase. A code with no session is 404
// "no_session", the same body whatever the reason. Only misses count toward the rate limit (S2, D20): once the caller's misses are
// used up, every lookup is 429 "too_many_attempts", a real code included.
[RegisterPerRequest]
public class GuestViews(
    AppDbContext db,
    IAudienceSessions sessions,
    IAudienceRelay relay,
    IJoinLookupLimiter limiter,
    IPublicUrls publicUrls,
    IDeckLooks looks)
{
    public const string NO_SESSION = "no_session";
    public const string TOO_MANY_ATTEMPTS = "too_many_attempts";

    public async Task<GuestView> GetAsync(string code, string? language, IPAddress? caller)
    {
        // The limit comes first, as on the hub (D20): a caller over it is refused for every code, a real one included, so
        // 429 says nothing about whether the code exists. Only a miss counts toward it.
        var key = CallerKey.For(caller);
        if (!limiter.Allow(key))
        {
            throw new TooManyRequestsException(TOO_MANY_ATTEMPTS);
        }
        var found = await sessions.FindAsync(code);
        if (found is null)
        {
            limiter.Miss(key);
            throw new NotFoundException(NO_SESSION);
        }
        limiter.Hit(key);

        var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == found.DeckId);
        var presenter = await db.Users.AsNoTracking().Where(u => u.Id == deck.OwnerId).Select(u => u.Name).SingleAsync();
        // The relay answers null only for a deck with no session, and the sessions just found one: that is a fault, not a
        // lobby to make up.
        var state = await relay.CurrentAsync(found.DeckId, language)
            ?? throw new InvalidOperationException($"The relay has no state for deck {found.DeckId}, which has a session.");
        return new GuestView(
            deck.Title,
            presenter,
            await looks.ForAsync(deck, CancellationToken.None),
            deck.Sharing == DeckSharing.Link ? publicUrls.Deck(deck.Id, deck.Slug) : null,
            state,
            // TODO: null until the deck's translation settings are read here; then the primary and the deck's languages.
            null);
    }
}
