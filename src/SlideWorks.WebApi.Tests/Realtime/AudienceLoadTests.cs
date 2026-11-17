using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Presenting;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// A room of guests on the real hub, the real registry and the real relay, over WebSockets. The relay holds a per-deck gate
// across its database reads and the registry awaits its observers inline, so joins arriving while the presenter moves
// could slow the moves. The presenter walks the deck first with nobody in the room, then again while GUESTS guests join in
// batches, one batch starting with each move and none waiting for the walk to begin. Each JoinAudience answer is timed on
// one monotonic stopwatch, and the test asserts that a good share of them answered between the first move and the last, so
// it fails if the joins stop overlapping the walk. Every move must be acknowledged within MOVE_BOUND. Sam owns the deck.
[TestFixture]
[CancelAfter(120000)]
public class AudienceLoadTests : ApiTestFixture
{
    private const int SLIDES = 6;
    private const int BATCHES = SLIDES - 1;
    private const int BATCH_SIZE = 40;
    private const int GUESTS = BATCHES * BATCH_SIZE;
    private const int MOVE_EVERY_MS = 200;
    private const double MIN_OVERLAP_SHARE = 0.25;
    private static readonly TimeSpan MOVE_BOUND = TimeSpan.FromSeconds(2);

    [Test]
    public async Task Guests_WhenTwoHundredJoinWhileThePresenterMoves_ShouldAllEndOnTheLastSlideWithEveryMoveAcknowledgedPromptly()
    {
        string deck;
        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam))
        {
            deck = await DeckSeed.CreateDeckAsync(agent, "Load check");
            for (var n = 1; n <= SLIDES; n++)
            {
                await DeckSeed.AddQuoteAsync(agent, deck, $"Quote number {n}.");
            }
        }
        var live = await Client.PostAsync($"/api/decks/{deck}/live", null);
        live.EnsureSuccessStatusCode();
        var code = (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldNotBeNull();
        await using var presenter = await PresentationStates.ConnectAsync(Factory, TestUser.Sam);
        await presenter.JoinAsync(deck, "k-load");

        // With nobody in the room, for comparison; the presenter ends back on slide 1.
        var quiet = await WalkAsync(presenter, deck, _ => { });
        await presenter.UpdateAsync(deck, 1, 0);

        // Batches of guests start with each move, the first at the same instant as the first move.
        var clock = Stopwatch.StartNew();
        var joins = new ConcurrentBag<(TimeSpan Asked, TimeSpan Answered)>();
        var guests = new List<Task<int>>();
        var loaded = await WalkAsync(presenter, deck, _ =>
        {
            for (var n = 0; n < BATCH_SIZE; n++)
            {
                guests.Add(Task.Run(() => FollowToTheEndAsync(code, clock, joins)));
            }
        }, clock);
        var finalSlides = await Task.WhenAll(guests);

        var firstMoveAsked = loaded[0].Asked;
        var lastMoveAnswered = loaded[^1].Answered;
        var duringTheWalk = joins.Count(join => join.Answered > firstMoveAsked && join.Answered < lastMoveAnswered);
        var answerTimes = joins.Select(join => join.Answered - join.Asked).Order().ToList();
        await TestContext.Progress.WriteLineAsync($"LOAD quiet moves ms: {Describe(quiet.Select(m => m.Took))}");
        await TestContext.Progress.WriteLineAsync($"LOAD loaded moves ms: {Describe(loaded.Select(m => m.Took))} with {GUESTS} guests joining in {BATCHES} batches");
        await TestContext.Progress.WriteLineAsync(
            $"LOAD join answer ms: median {Ms(answerTimes[answerTimes.Count / 2])} max {Ms(answerTimes[^1])}; "
            + $"{duringTheWalk} of {joins.Count} answered between the first and the last move (walk {Ms(lastMoveAnswered - firstMoveAsked)} ms)");
        finalSlides.Length.ShouldBe(GUESTS);
        finalSlides.ShouldAllBe(slide => slide == SLIDES);
        joins.Count.ShouldBe(GUESTS);
        loaded.Count.ShouldBe(BATCHES);
        duringTheWalk.ShouldBeGreaterThanOrEqualTo((int)(GUESTS * MIN_OVERLAP_SHARE), "the joins should be in flight while the presenter moves");
        loaded.ShouldAllBe(move => move.Took < MOVE_BOUND);
    }

    // Moves slide 1 to SLIDES, one slide at a time with MOVE_EVERY_MS between moves and none before the first. beforeMove runs
    // as each move is asked. Returns when each move was asked and answered, on clock, and how long it took.
    private static async Task<IReadOnlyList<(TimeSpan Asked, TimeSpan Answered, TimeSpan Took)>> WalkAsync(
        PresentationStates presenter, string deck, Action<int> beforeMove, Stopwatch? clock = null)
    {
        clock ??= Stopwatch.StartNew();
        var moves = new List<(TimeSpan Asked, TimeSpan Answered, TimeSpan Took)>();
        for (var slide = 2; slide <= SLIDES; slide++)
        {
            if (slide > 2)
            {
                await Task.Delay(MOVE_EVERY_MS);
            }
            beforeMove(slide);
            var asked = clock.Elapsed;
            await presenter.UpdateAsync(deck, slide, 0);
            var answered = clock.Elapsed;
            moves.Add((asked, answered, answered - asked));
        }
        return moves;
    }

    // One guest: connects, joins, and reads states until it has seen the last slide. Returns the slide it ended on.
    private async Task<int> FollowToTheEndAsync(string code, Stopwatch clock, ConcurrentBag<(TimeSpan Asked, TimeSpan Answered)> joins)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var asked = clock.Elapsed;
        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        joins.Add((asked, clock.Elapsed));
        var (seq, slide) = (joined.GetProperty("seq").GetInt64(), joined.GetProperty("current").GetProperty("index").GetInt32());
        while (slide != SLIDES)
        {
            var next = await guest.NextAudienceAsync();
            if (next.GetProperty("type").GetString() != "audienceState" || next.GetProperty("seq").GetInt64() <= seq)
            {
                continue;
            }
            (seq, slide) = (next.GetProperty("seq").GetInt64(), next.GetProperty("current").GetProperty("index").GetInt32());
        }
        return slide;
    }

    private static string Describe(IEnumerable<TimeSpan> took)
    {
        var list = took.ToList();
        return $"[{string.Join(", ", list.Select(Ms))}] max {Ms(list.Max())}";
    }

    private static string Ms(TimeSpan span) => span.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
}
