using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// The audience's reactions (Q7, D6, D19). Taps are counted in memory against the slide on screen, published as reactions
// (ReactionsBurst) at most four times a second per deck, and written as totals per session, slide and emoji every 5 seconds
// and when the session ends; a guest's first tap of a session writes its ReactionGuest row. Never a row per tap, never sent
// to phones. A singleton: a call that needs the database opens its own scope.
public interface IReactions
{
    // Code is the audience session's join code, guestId the sw_guest cookie's value and emoji the body's string, so an unknown
    // one is Invalid here.
    Task<ReactionResult> TapAsync(string code, string guestId, string? emoji);

    // The session's totals per emoji: what is stored plus what is still in memory.
    Task<ReactionCounts> TotalsAsync(Guid sessionId);

    // Ending the live session: writes what is still in memory. Called once from LiveCommands.EndAsync before the session ends.
    Task EndSessionAsync(Guid sessionId);
}
