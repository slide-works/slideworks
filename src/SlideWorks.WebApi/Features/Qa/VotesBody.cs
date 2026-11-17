using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Qa;

// The body of PUT /api/join/{code}/questions/votes (question-votes.json "request", Q4), bound by the framework once
// BodyLimitAttribute has read at most MAX_BYTES of it (else 413). Every member is required, so a missing revision is the
// framework's 400, never a first vote (revision 0). Numbers are strict: a pick or revision written as a string ("1") is the
// framework's 400, as every number on the wire is an integer. Check refuses more than MAX_PICKS picks before the seam sees them and
// gives the seam's VoteRequest; the picks' rules (unknown question, duplicate) are IGuestQuestions', which knows the round.
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record VotesBody([property: JsonRequired] string Round, [property: JsonRequired] IReadOnlyList<int> Picks, [property: JsonRequired] int Revision)
{
    // The largest body the votes route reads: 1,000 picks of up to four digits, with the round and the revision.
    public const int MAX_BYTES = 16 * 1024;

    // A session holds a few hundred questions, so a guest can't upvote more than this many.
    public const int MAX_PICKS = 1000;

    public const string TOO_MANY_PICKS = "picks: at most 1000";

    public VoteRequest Check()
        => Picks.Count > MAX_PICKS ? throw new BadRequestException(TOO_MANY_PICKS) : new VoteRequest(Round, Picks, Revision);
}
