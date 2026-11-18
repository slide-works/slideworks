using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The real reactions (Q7) through POST /api/join/{code}/reactions: the roster, the room's reactions and the 10-in-5-seconds
// limit, and the bursts on the bus. Each test seeds a live deck of its own (QaSeed, the presenter on the quote slide) with
// guests of its own, joined through the hub. The fixture's fake clock drives the limit's window and the burst interval, so
// nothing sleeps: a burst the clock allows runs on the thread pool, and the tests wait for it with a bounded wait. A tap's
// counts are read back through GET /api/decks/{id}/qa, which adds what is in memory to what is stored.
[TestFixture]
[CancelAfter(60000)]
public class ReactionTests : QaTestFixture
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);

    private IAudienceEvents Bus => Factory.Services.GetRequiredService<IAudienceEvents>();

    [Test]
    public async Task TapAsync_WhenEleventhInFiveSeconds_ShouldBeLimited()
    {
        var qa = await SeedQaAsync();
        await using var guest = await JoinAsync(qa.Code);

        var first = new List<HttpStatusCode>();
        for (var tap = 0; tap < ReactionLimiter.PER_WINDOW; tap++)
        {
            first.Add((await guest.ReactAsync(qa.Code, new ReactionRequest("clap"))).StatusCode);
        }
        var eleventh = await guest.ReactAsync(qa.Code, new ReactionRequest("clap"));
        Clock.Advance(ReactionLimiter.WINDOW);
        var afterTheWindow = await guest.ReactAsync(qa.Code, new ReactionRequest("clap"));

        first.ShouldAllBe(status => status == HttpStatusCode.NoContent);
        eleventh.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await ErrorOf(eleventh)).ShouldBe(GuestQaErrors.TOO_MANY_REACTIONS);
        afterTheWindow.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await TotalsAsync(qa.DeckId)).ShouldBe(ReactionCounts.Zero with { Clap = ReactionLimiter.PER_WINDOW + 1 });
    }

    [Test]
    public async Task TapAsync_WhenReactionsOffOnThisSlide_ShouldBeRefused()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SwitchAsync(qa.QuoteSlideId, new QaSlideSwitch(true, false));
        await using var guest = await JoinAsync(qa.Code);

        var response = await guest.ReactAsync(qa.Code, new ReactionRequest("heart"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorOf(response)).ShouldBe(GuestQaErrors.REACTIONS_OFF);
        (await TotalsAsync(qa.DeckId)).ShouldBe(ReactionCounts.Zero);
    }

    [Test]
    public async Task TapAsync_WhenReactionsOffForTheDeck_ShouldBeRefused()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SettingsAsync(qa.DeckId, QaSettings.Default with { Reactions = false });
        await using var guest = await JoinAsync(qa.Code);

        var response = await guest.ReactAsync(qa.Code, new ReactionRequest("heart"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorOf(response)).ShouldBe(GuestQaErrors.REACTIONS_OFF);
        (await TotalsAsync(qa.DeckId)).ShouldBe(ReactionCounts.Zero);
    }

    [Test]
    public async Task TapAsync_WhenGuestNotOnRoster_ShouldBeNoSession()
    {
        var qa = await SeedQaAsync();
        await using var stranger = await GuestClient.ConnectAsync(Factory);

        var response = await stranger.ReactAsync(qa.Code, new ReactionRequest("wow"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ErrorOf(response)).ShouldBe("no_session");
        (await TotalsAsync(qa.DeckId)).ShouldBe(ReactionCounts.Zero);
    }

    // Forty taps from four guests at one instant: the first goes out at once, the rest wait for the burst interval and go
    // out together, so the deck gets two bursts, never one per tap, and every tap is in one of them.
    [Test]
    public async Task TapAsync_WhenFortyArriveAtOnce_ShouldPublishAtMostFourBatchesASecond()
    {
        var qa = await SeedQaAsync();
        var bursts = new BurstRecorder();
        using var subscription = Bus.Subscribe(qa.DeckId, bursts.OnEventAsync);
        var guests = new List<GuestClient>();
        for (var g = 0; g < 4; g++)
        {
            guests.Add(await JoinAsync(qa.Code));
        }

        try
        {
            var taps = await Task.WhenAll(guests.SelectMany(guest => Enumerable.Range(0, ReactionLimiter.PER_WINDOW).Select(_ => guest.ReactAsync(qa.Code, new ReactionRequest("clap")))));
            await WithinAsync(bursts.CountReachedAsync(1));
            var atOnce = bursts.Count;
            Clock.Advance(Reactions.BurstInterval - TimeSpan.FromMilliseconds(1));
            var justBeforeTheInterval = bursts.Count;
            Clock.Advance(TimeSpan.FromMilliseconds(1));
            await WithinAsync(bursts.CountReachedAsync(2));
            Clock.Advance(Reactions.BurstInterval);
            Clock.Advance(Reactions.BurstInterval);
            Clock.Advance(Reactions.BurstInterval);
            var inTheSecond = bursts.Count;
            var all = bursts.All;

            taps.ShouldAllBe(response => response.StatusCode == HttpStatusCode.NoContent);
            atOnce.ShouldBe(1);
            justBeforeTheInterval.ShouldBe(1);
            inTheSecond.ShouldBe(2);
            all.ShouldAllBe(b => b.Slide == qa.QuoteSlide);
            all.Sum(b => b.Counts.Clap).ShouldBe(40);
        }
        finally
        {
            foreach (var guest in guests)
            {
                await guest.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task Frames_WhenReactionsPublished_ShouldNotReachAGuest()
    {
        var qa = await SeedQaAsync();
        await using var guest = await JoinAsync(qa.Code);
        await using var owner = await HubReceiver.StartAsync(Factory, SignedInAs);
        await owner.Connection.InvokeAsync("WatchAudience", qa.DeckId);

        var tap = await guest.ReactAsync(qa.Code, new ReactionRequest("laugh"));
        var toOwner = await HubReceiver.NextQaEventAsync(owner.NextAudienceAsync);
        await Bus.PublishAsync(new QuestionTally(qa.DeckId, qa.RoundId.ToString(), 1, []));
        var guestFirst = await HubReceiver.NextQaEventAsync(guest.NextAudienceAsync);

        tap.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        toOwner.GetProperty("type").GetString().ShouldBe("reactions");
        toOwner.GetProperty("slide").GetInt32().ShouldBe(qa.QuoteSlide);
        toOwner.GetProperty("counts").GetProperty("laugh").GetInt32().ShouldBe(1);
        // The guest's first Q&A frame is the tally published after the burst: the burst never reached the phone.
        guestFirst.GetProperty("type").GetString().ShouldBe("questionTally");
        guestFirst.GetRawText().ShouldNotContain("laugh", Case.Sensitive);
    }

    private async Task<GuestClient> JoinAsync(string code)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        return guest;
    }

    private async Task<ReactionCounts> TotalsAsync(string deckId)
    {
        var body = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deckId}/qa");
        return body.GetProperty("reactions").Deserialize<ReactionCounts>(JsonSerializerOptions.Web)!;
    }

    private static async Task<string> ErrorOf(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()!;

    private static async Task WithinAsync(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(_wait)) != task)
        {
            throw new TimeoutException($"No burst within {_wait.TotalSeconds} seconds.");
        }
        await task;
    }

    // The reactions events a deck's subscription saw, and a wait for the count to reach a number.
    private sealed class BurstRecorder
    {
        private readonly Lock _gate = new();
        private readonly List<ReactionsBurst> _bursts = [];
        private readonly List<(int Count, TaskCompletionSource Reached)> _waiters = [];

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _bursts.Count;
                }
            }
        }

        public IReadOnlyList<ReactionsBurst> All
        {
            get
            {
                lock (_gate)
                {
                    return [.. _bursts];
                }
            }
        }

        public Task OnEventAsync(AudienceEvent audienceEvent)
        {
            if (audienceEvent is ReactionsBurst burst)
            {
                lock (_gate)
                {
                    _bursts.Add(burst);
                    foreach (var waiter in _waiters.Where(w => w.Count <= _bursts.Count))
                    {
                        waiter.Reached.TrySetResult();
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task CountReachedAsync(int count)
        {
            lock (_gate)
            {
                var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (_bursts.Count >= count)
                {
                    reached.SetResult();
                }
                _waiters.Add((count, reached));
                return reached.Task;
            }
        }
    }
}
