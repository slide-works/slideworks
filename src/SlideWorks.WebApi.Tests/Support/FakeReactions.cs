using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IReactions for tests that need reactions without the real counting: each test sets the result a join code's taps answer
// with (Counted when none is set) and a session's totals (zero when none are set). It records every tap's code, guest and
// emoji and every session ended, in order, and counts nothing. Add it with FakeReactions.AddTo. The app, and so this fake,
// is shared by every test in a fixture: each test uses codes and sessions of its own, or calls Reset when it is done.
public sealed class FakeReactions : IReactions
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ReactionResult> _results = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ReactionCounts> _totals = [];
    private readonly List<(string Code, string GuestId, string? Emoji)> _taps = [];
    private readonly List<Guid> _ended = [];

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeReactions>().AddSingleton<IReactions>(provider => provider.GetRequiredService<FakeReactions>());

    public void SetTap(string code, ReactionResult result)
    {
        lock (_gate)
        {
            _results[code] = result;
        }
    }

    public void SetTotals(Guid sessionId, ReactionCounts totals)
    {
        lock (_gate)
        {
            _totals[sessionId] = totals;
        }
    }

    // Every tap for this code, in order.
    public IReadOnlyList<(string GuestId, string? Emoji)> TapsFor(string code)
    {
        lock (_gate)
        {
            return [.. _taps.Where(t => t.Code == code).Select(t => (t.GuestId, t.Emoji))];
        }
    }

    // How many times EndSessionAsync was called for the session.
    public int EndsOf(Guid sessionId)
    {
        lock (_gate)
        {
            return _ended.Count(s => s == sessionId);
        }
    }

    // Forgets every result, total and call.
    public void Reset()
    {
        lock (_gate)
        {
            _results.Clear();
            _totals.Clear();
            _taps.Clear();
            _ended.Clear();
        }
    }

    public Task<ReactionResult> TapAsync(string code, string guestId, string? emoji)
    {
        lock (_gate)
        {
            _taps.Add((code, guestId, emoji));
            return Task.FromResult(_results.GetValueOrDefault(code) ?? new ReactionResult.Counted());
        }
    }

    public Task<ReactionCounts> TotalsAsync(Guid sessionId)
    {
        lock (_gate)
        {
            return Task.FromResult(_totals.GetValueOrDefault(sessionId, ReactionCounts.Zero));
        }
    }

    public Task EndSessionAsync(Guid sessionId)
    {
        lock (_gate)
        {
            _ended.Add(sessionId);
        }
        return Task.CompletedTask;
    }
}
