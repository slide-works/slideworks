using System.Text.Json;

namespace SlideWorks.AcceptanceTests.Journeys.Support;

// A deck built through the agent's tools and gone live through the API, as the owner does, for the journeys that follow
// a live talk on a phone. Small on purpose, and only ever added to: SW-6's and SW-9's journeys both use it.
public sealed record LiveDeck(string DeckId, string Code)
{
    // A quote slide, then a qa slide with no title (the client draws "Questions from the room").
    public static IReadOnlyList<object> DefaultSlides { get; } =
    [
        new { layout = "quote", slots = new { quote = "Start small.", attribution = "Sam Rivera" } },
        new { layout = "qa", slots = new { } },
    ];

    // Creates a deck for the agent's presenter with DefaultSlides and goes live as owner, the presenter's signed-in page.
    public static Task<LiveDeck> StartAsync(AgentSession agent, IPage owner) => StartAsync(agent, owner, DefaultSlides);

    // Creates a deck with the given slides, each an add_slide argument object without its deck ({ layout, slots }), in
    // order, and goes live as owner. Returns the deck and the join code Go live gave.
    public static async Task<LiveDeck> StartAsync(AgentSession agent, IPage owner, IReadOnlyList<object> slides)
    {
        var deckId = (await agent.CallAsync("create_deck", new { title = $"Live {Guid.NewGuid():N}" })).GetProperty("deck").GetString()!;
        foreach (var slide in slides)
        {
            var args = JsonSerializer.SerializeToNode(slide)!.AsObject();
            args["deck"] = deckId;
            await agent.CallAsync("add_slide", args);
        }

        var response = await owner.APIRequest.PostAsync($"/api/decks/{deckId}/live", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(200, await response.TextAsync());
        var live = JsonDocument.Parse(await response.TextAsync()).RootElement;
        return new LiveDeck(deckId, live.GetProperty("code").GetString()!);
    }

    // A new signed-out browser context the size of a phone (390×844) on the app. The caller disposes it.
    public static Task<IBrowserContext> OpenPhoneAsync(IBrowser browser) =>
        browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = AcceptanceApp.BaseUrl,
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        });
}
