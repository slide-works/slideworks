using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The arrival rule against SW-4's ordering (V4): the registry notifies its observers outside its per-deck lock, so states
// can reach them out of order and concurrently. Arrival acts on the presenter's position as the registry holds it when its
// turn comes, so whatever order the notifications land in, the only round left open is the one on the presenter's actual
// slide. These run the real IPolls, so the end state is read back through the presenter's poll route. The registry is
// SettablePresentations: a test sets the position and then delivers whatever states it likes to every observer, as the
// registry does after it releases its lock. Each test makes its own deck.
[TestFixture]
[CancelAfter(120000)]
public class PollArrivalOrderTests : PollTestFixture
{
    private const int CHANGES = 24;

    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(30);

    // The presenter went 2 then 3, both on-arrival polls, and the registry is on 3; the two notifications land 3 then 2.
    [Test]
    public async Task Presenter_WhenStatesForAdjacentPollsArriveInReverse_ShouldLeaveThePresentersSlideOpen()
    {
        var poll = await SeedPollAsync();
        var next = await AddPollAsync(poll.DeckId);
        Presentations.Set(SettablePresentations.At(poll.DeckId, next, seq: 2));

        await NotifyAsync(SettablePresentations.At(poll.DeckId, next, seq: 2));
        await NotifyAsync(SettablePresentations.At(poll.DeckId, poll.Slide, seq: 1));

        (await PhaseAsync(poll.DeckId, next)).ShouldBe("open");
        (await PhaseAsync(poll.DeckId, poll.Slide)).ShouldBe("draft");
    }

    // Many moves across the deck, each set on the registry and then notified at once, so the notifications run concurrently
    // and in any order. The last move is to a poll no earlier move reached: it must end open, and nothing else open.
    [Test]
    [Repeat(10)]
    public async Task Presenter_WhenManyStatesForOneDeckArriveConcurrently_ShouldLeaveOnlyTheCurrentSlidesPollOpen()
    {
        var poll = await SeedPollAsync();
        var third = await AddPollAsync(poll.DeckId);
        var last = await AddPollAsync(poll.DeckId);
        int[] visited = [poll.OtherSlide, poll.Slide, third];

        var notifications = new List<Task>();
        for (var i = 0; i < CHANGES; i++)
        {
            var state = SettablePresentations.At(poll.DeckId, visited[i % visited.Length], seq: i + 1);
            Presentations.Set(state);
            notifications.Add(Task.Run(() => NotifyAsync(state)));
        }
        var final = SettablePresentations.At(poll.DeckId, last, seq: CHANGES + 1);
        Presentations.Set(final);
        notifications.Add(Task.Run(() => NotifyAsync(final)));
        var all = Task.WhenAll(notifications);
        var first = await Task.WhenAny(all, Task.Delay(_bound));

        first.ShouldBe(all, "the notifications did not all finish within the bound");
        await all;

        (await PhaseAsync(poll.DeckId, last)).ShouldBe("open");
        (await PhaseAsync(poll.DeckId, poll.Slide)).ShouldNotBe("open");
        (await PhaseAsync(poll.DeckId, third)).ShouldNotBe("open");
    }

    // What the registry does after it releases its lock: tell every observer, in turn, about one state.
    private async Task NotifyAsync(PresentationState state)
    {
        foreach (var observer in Factory.Services.GetServices<IPresentationObserver>())
        {
            await observer.OnChangedAsync(state);
        }
    }

    // Appends an on-arrival poll slide to the deck. Returns its number.
    private async Task<int> AddPollAsync(string deckId)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var added = await DeckSeed.AddSlideAsync(agent, deckId, "poll", JsonSerializer.Serialize(new { question = "Which day do we ship?", options = new[] { "Tuesday", "Friday" } }));
        return added.GetProperty("slide").GetInt32();
    }

    private async Task<string> PhaseAsync(string deckId, int slide)
    {
        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deckId}/polls/{slide}");
        return view.GetProperty("phase").GetString()!;
    }
}
