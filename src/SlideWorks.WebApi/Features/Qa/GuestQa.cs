using System.Net;
using Agoda.IoC.Core;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.RateLimits;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Qa;

// The guest Q&A and reaction routes' checks over the seams (Q4, Q7), as GuestPolls does for a poll, in order: a caller whose
// join-code misses are used up is 429 too_many_attempts, a real code included, with the body GET /api/join/{code} gives (J3,
// D20); no sw_guest cookie is 400 no_guest; a body over its limit (BodyLimitAttribute) or one that isn't JSON was refused
// before this runs. Then this resolves the code itself: a code with no session is 404 no_session and a miss toward the budget
// GET /api/join/{code} and JoinAudience share, and the seam is never called. A known code never counts, so a guest not on the
// session's roster (the seam's NoSession, or a null view) is 404 no_session and no miss. These routes use the limiter's
// miss-only path, as the poll routes do: they hold no in-flight slot, so a room behind one address is never refused for the
// requests it has in flight.
[RegisterPerRequest]
public class GuestQa(
    IAudienceSessions sessions,
    IJoinLookupLimiter limiter,
    IGuestQuestions questions,
    IQuestionAsking asking,
    IReactions reactions)
{
    public async Task<GuestQuestionsView> ViewAsync(string code, string? guestId, IPAddress? caller)
    {
        var guest = await SeatAsync(code, guestId, caller);
        return await questions.ViewAsync(code, guest) ?? throw new NotFoundException(GuestViews.NO_SESSION);
    }

    public async Task<AskResult> AskAsync(string code, string? guestId, AskRequest request, IPAddress? caller)
        => await asking.AskAsync(code, await SeatAsync(code, guestId, caller), request);

    public async Task<VoteResult> VoteAsync(string code, string? guestId, VotesBody body, IPAddress? caller)
    {
        var key = Allowed(caller);
        var guest = Require(guestId);
        var request = body.Check();
        await KnownAsync(code, key);
        return await questions.VoteAsync(code, guest, request);
    }

    public async Task<ReactionResult> TapAsync(string code, string? guestId, ReactionRequest request, IPAddress? caller)
        => await reactions.TapAsync(code, await SeatAsync(code, guestId, caller), request.Emoji);

    // The guest's id once the caller is within its miss budget, carries the cookie and names a code that has a session.
    private async Task<string> SeatAsync(string code, string? guestId, IPAddress? caller)
    {
        var key = Allowed(caller);
        var guest = Require(guestId);
        await KnownAsync(code, key);
        return guest;
    }

    private string Allowed(IPAddress? caller)
    {
        var key = CallerKey.For(caller);
        return limiter.OverMissLimit(key) ? throw new TooManyRequestsException(GuestViews.TOO_MANY_ATTEMPTS) : key;
    }

    private static string Require(string? guestId) => guestId ?? throw new BadRequestException(GuestPolls.NO_GUEST);

    // An unknown code is a miss and 404 no_session; a known one, in any phase, counts for nothing.
    private async Task KnownAsync(string code, string callerKey)
    {
        if (await sessions.FindAsync(code) is null)
        {
            limiter.RecordMiss(callerKey);
            throw new NotFoundException(GuestViews.NO_SESSION);
        }
    }
}
