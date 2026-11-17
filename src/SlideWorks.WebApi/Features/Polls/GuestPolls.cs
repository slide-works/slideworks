using System.Net;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.RateLimits;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls;

// The guest poll routes' checks over IPollAnswers (V5), in order: a caller whose join-code misses are used up is 429
// too_many_attempts, a real code included, with the body GET /api/join/{code} gives (J3, D20); no sw_guest cookie is 400
// no_guest; an answer body over 1 KB is 413 and a malformed one 400 (AnswerBody). Nothing before the seam reads or writes poll data. A code with no
// session is 404 no_session and counts as a miss toward the budget GET /api/join/{code} and JoinAudience share. These routes
// use the limiter's miss-only path: an answer waits for a batched commit, and a room behind one address must never be
// refused for the lookups it has in flight, so they hold no in-flight slot, and a known code never counts.
[RegisterPerRequest]
public class GuestPolls(IPollAnswers answers, IJoinLookupLimiter limiter)
{
    public const string NO_GUEST = "no_guest";

    public async Task<GuestPollView> ViewAsync(string code, string? guestId, IPAddress? caller)
    {
        var key = Allowed(caller);
        var view = await answers.ViewAsync(code, Require(guestId));
        if (view is null)
        {
            limiter.RecordMiss(key);
            throw new NotFoundException(GuestViews.NO_SESSION);
        }
        return view;
    }

    public async Task<AnswerResult> SubmitAsync(string code, string? guestId, Stream body, IPAddress? caller)
    {
        var key = Allowed(caller);
        var guest = Require(guestId);
        var result = await answers.SubmitAsync(code, guest, await AnswerBody.ReadAsync(body));
        if (result is AnswerResult.NoSession)
        {
            limiter.RecordMiss(key);
        }
        return result;
    }

    private string Allowed(IPAddress? caller)
    {
        var key = CallerKey.For(caller);
        return limiter.OverMissLimit(key) ? throw new TooManyRequestsException(GuestViews.TOO_MANY_ATTEMPTS) : key;
    }

    private static string Require(string? guestId) => guestId ?? throw new BadRequestException(NO_GUEST);
}
