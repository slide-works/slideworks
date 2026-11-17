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
// across its database reads and the registry awaits its observers inline, so a burst of joins could slow the presenter's
// moves: this measures how long each move takes to be acknowledged, first with nobody in the room and then while 40
// guests are joining. Sam owns the deck.
[TestFixture]
[CancelAfter(120000)]
public class AudienceLoadTests : ApiTestFixture
{
    private const int SLIDES = 5;
    private const int GUESTS = 40;
    private const int MOVE_EVERY_MS = 200;
    private static readonly TimeSpan MOVE_BOUND = TimeSpan.FromSeconds(2);

    [Test]
    public async Task Guests_WhenFortyJoinWhileThePresenterMoves_ShouldAllEndOnTheLastSlideWithEveryMoveAcknowledgedPromptly()
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
        var quiet = await WalkAsync(presenter, deck);
        await presenter.UpdateAsync(deck, 1, 0);

        // Every guest connects and joins at once, and the presenter's walk starts as they do.
        var joining = Enumerable.Range(0, GUESTS).Select(_ => FollowToTheEndAsync(code)).ToList();
        var loaded = await WalkAsync(presenter, deck);
        var finalSlides = await Task.WhenAll(joining);

        await TestContext.Out.WriteLineAsync($"LOAD quiet moves ms: {Describe(quiet)}");
        await TestContext.Out.WriteLineAsync($"LOAD loaded moves ms: {Describe(loaded)} with {GUESTS} guests joining");
        finalSlides.ShouldAllBe(slide => slide == SLIDES);
        loaded.Count.ShouldBe(SLIDES - 1);
        loaded.ShouldAllBe(took => took < MOVE_BOUND);
    }

    // Moves slide 1 to SLIDES one slide every MOVE_EVERY_MS, and returns how long each UpdatePresentation took to answer.
    private static async Task<IReadOnlyList<TimeSpan>> WalkAsync(PresentationStates presenter, string deck)
    {
        var took = new List<TimeSpan>();
        for (var slide = 2; slide <= SLIDES; slide++)
        {
            await Task.Delay(MOVE_EVERY_MS);
            var clock = Stopwatch.StartNew();
            await presenter.UpdateAsync(deck, slide, 0);
            took.Add(clock.Elapsed);
        }
        return took;
    }

    // One guest: connects, joins, and reads states until it has seen the last slide. Returns the slide it ended on.
    private async Task<int> FollowToTheEndAsync(string code)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
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

    private static string Describe(IReadOnlyList<TimeSpan> took)
        => $"[{string.Join(", ", took.Select(t => t.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)))}] max {took.Max().TotalMilliseconds:F1}";
}
