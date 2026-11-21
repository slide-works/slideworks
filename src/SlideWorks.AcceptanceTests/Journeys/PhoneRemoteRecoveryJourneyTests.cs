using System.Text.Json;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// The phone remote's access and failure paths on the real app and the real hub (P2, P8): someone who doesn't own the
// deck opens its speaker mode and is turned away without touching the talk, and the owner's phone joins late, loses
// its socket and picks the talk up again where the presenter now is. Sam presents from this test's page; the phone and
// the other person each get their own browser context, so no session is swapped on a page.
public class PhoneRemoteRecoveryJourneyTests : AcceptanceTest
{
    private const int SLIDES = 10;
    private static readonly TimeSpan FRAME_TIMEOUT = TimeSpan.FromSeconds(15);

    [Test]
    public async Task SpeakerMode_WhenSomeoneElseOpensTheRemote_ShouldShowNotYoursAndLeaveTheConnectedListUnchanged()
    {
        var deckId = await SeedDeckAsync();
        var presenter = await OpenPresenterAsync(deckId);
        await Expect(presenter.Chips).ToHaveCountAsync(0);
        await Expect(presenter.NoAudienceChip).ToBeVisibleAsync();

        await using var otherContext = await NewPhoneContextAsync(TestUser.Alex);
        var otherPage = await otherContext.NewPageAsync();
        var joinRefused = WatchJoinRefusal(otherPage);
        var other = new SpeakerPage(otherPage);
        await other.GotoAsync(deckId);

        await Expect(other.NotYours).ToBeVisibleAsync();
        await Expect(other.NotYours).ToHaveTextAsync("This presentation isn't yours, or it doesn't exist");
        await Expect(other.Now).ToHaveCountAsync(0);
        await Expect(other.Notes).ToHaveCountAsync(0);
        var first = await Task.WhenAny(joinRefused, Task.Delay(FRAME_TIMEOUT));
        (first == joinRefused).ShouldBeTrue("The other person's JoinPresentation never came back refused with 'Deck not found'.");

        // The refused join changed nothing: the presenter still shows no audience window and no phone.
        await Expect(presenter.Chips).ToHaveCountAsync(0);
        await Expect(presenter.NoAudienceChip).ToBeVisibleAsync();

        // The owner's phone joins next. The state it broadcasts carries the whole connected list, so a phone entry left
        // by the refused join would show here as a second chip.
        await using var phoneContext = await NewPhoneContextAsync(TestUser.Sam);
        var speaker = new SpeakerPage(await phoneContext.NewPageAsync());
        await speaker.GotoAsync(deckId);
        await speaker.IsLiveAsync();
        await Expect(presenter.PhoneChips).ToHaveCountAsync(1);
        await Expect(presenter.Chips).ToHaveCountAsync(1);
    }

    [Test]
    public async Task SpeakerMode_WhenThePhoneJoinsLateAndItsSocketDrops_ShouldStartAndResumeOnThePresentersSlide()
    {
        var deckId = await SeedDeckAsync();
        var presenter = await OpenPresenterAsync(deckId);
        await presenter.JumpToAsync(8);
        await Expect(presenter.Count).ToHaveTextAsync($"8 / {SLIDES}");

        await using var phoneContext = await NewPhoneContextAsync(TestUser.Sam);
        var phonePage = await phoneContext.NewPageAsync();
        await DroppableHub.TrackAsync(phonePage);
        var speaker = new SpeakerPage(phonePage);
        await speaker.GotoAsync(deckId);

        // Late join: the phone starts where the presenter already is.
        await speaker.IsLiveAsync();
        await Expect(speaker.Now).ToHaveAttributeAsync("data-slide", "8");
        await Expect(speaker.Count).ToHaveTextAsync($"8 / {SLIDES}");
        await Expect(presenter.PhoneChips).ToHaveCountAsync(1);

        // Hold the phone's reconnect at its negotiate request, then drop its socket.
        var reconnect = await HeldNegotiate.RouteAsync(phonePage);
        (await DroppableHub.CloseOpenAsync(phonePage)).ShouldBe(1, "The phone should have had one open hub WebSocket to drop.");

        await Expect(speaker.Reconnecting).ToBeVisibleAsync();
        await Expect(speaker.Status).ToHaveAttributeAsync("data-status", "reconnecting");
        await reconnect.AskedAsync(1);
        await Expect(presenter.PhoneChips).ToHaveCountAsync(0);

        // The talk moves on while the phone is away.
        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync($"9 / {SLIDES}");

        reconnect.Release();

        // The phone rejoins with its last state as resume; the live state wins, so it lands on the presenter's slide.
        await speaker.IsLiveAsync();
        await Expect(speaker.Reconnecting).ToHaveCountAsync(0);
        await Expect(speaker.Now).ToHaveAttributeAsync("data-slide", "9");
        await Expect(speaker.Count).ToHaveTextAsync($"9 / {SLIDES}");
        await Expect(presenter.PhoneChips).ToHaveCountAsync(1);
        await Expect(presenter.Chips).ToHaveCountAsync(1);
    }

    // A deck of Sam's with SLIDES slides, made the way an agent makes one (create_deck, then add_slide over MCP).
    private static async Task<string> SeedDeckAsync()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Phone Recovery {Guid.NewGuid():N}" });
        var deckId = created.GetProperty("deck").GetString()!;
        var existing = (await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = deckId })).GetProperty("slides").GetArrayLength();
        var quotes = Enumerable.Range(existing + 1, SLIDES - existing).Select(n => $"Quote for slide {n}").ToArray();
        await AgentSeed.AddQuotesAsync(TestUser.Sam, deckId, quotes);
        return deckId;
    }

    // Signs this test's page in as Sam and opens the deck's presenter window, joined and ready for its keys.
    private async Task<PresenterPage> OpenPresenterAsync(string deckId)
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        return presenter;
    }

    // A phone-sized browser context of its own, signed in as the user.
    private async Task<IBrowserContext> NewPhoneContextAsync(TestUser user)
    {
        var context = await Browser.NewContextAsync(new()
        {
            BaseURL = AcceptanceApp.BaseUrl,
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        });
        await SignIn.AsAsync(await context.NewPageAsync(), user);
        return context;
    }

    // Completes when the page's hub connection gets a JoinPresentation refused with "Deck not found": the frames the
    // page sends name the call's invocation id, and its completion comes back with that id and an error.
    private static Task WatchJoinRefusal(IPage page)
    {
        var refused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var joins = new HashSet<string>();
        page.WebSocket += (_, socket) =>
        {
            socket.FrameSent += (_, frame) =>
            {
                foreach (var message in HubMessages(frame.Text))
                {
                    if (message.TryGetProperty("target", out var target) && target.GetString() == "JoinPresentation"
                        && message.TryGetProperty("invocationId", out var id))
                    {
                        lock (joins)
                        {
                            joins.Add(id.GetString()!);
                        }
                    }
                }
            };
            socket.FrameReceived += (_, frame) =>
            {
                foreach (var message in HubMessages(frame.Text))
                {
                    if (message.TryGetProperty("invocationId", out var id) && message.TryGetProperty("error", out var error)
                        && (error.GetString() ?? string.Empty).EndsWith("Deck not found", StringComparison.Ordinal))
                    {
                        lock (joins)
                        {
                            if (joins.Contains(id.GetString()!))
                            {
                                refused.TrySetResult();
                            }
                        }
                    }
                }
            };
        };
        return refused.Task;
    }

    // The SignalR JSON protocol's messages in one frame: JSON objects, each ended by the record separator.
    private static IEnumerable<JsonElement> HubMessages(string? frame) =>
        (frame ?? string.Empty)
            .Split('\u001e', StringSplitOptions.RemoveEmptyEntries)
            .Select(record => JsonSerializer.Deserialize<JsonElement>(record))
            .Where(message => message.ValueKind == JsonValueKind.Object);
}
