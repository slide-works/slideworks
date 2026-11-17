using Agoda.IoC.Core;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// TODO: replace with the real reactions: the roster, the room, the limiter, the counts in memory, the batches and the totals.
// Accepts every tap and counts nothing: every session's totals are zero.
[RegisterSingleton(For = typeof(IReactions))]
public sealed class StubReactions : IReactions
{
    public Task<ReactionResult> TapAsync(string code, string guestId, string? emoji) => Task.FromResult<ReactionResult>(new ReactionResult.Counted());

    public Task<ReactionCounts> TotalsAsync(Guid sessionId) => Task.FromResult(ReactionCounts.Zero);

    public Task EndSessionAsync(Guid sessionId) => Task.CompletedTask;
}
