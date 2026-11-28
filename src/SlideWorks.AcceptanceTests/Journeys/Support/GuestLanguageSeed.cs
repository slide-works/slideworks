using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys.Support;

// Sam's translated decks for the guest language journeys, seeded the way an agent does it: create_deck and add_slide, then
// set_translations to turn Thai on and translate_slides to write its lines. Nothing here touches the database.
public static class GuestLanguageSeed
{
    // A phone's browser context at 390 by 844 whose own language is the locale (navigator.languages follows it).
    public static Task<IBrowserContext> OpenPhoneContextAsync(IBrowser browser, string locale) => browser.NewContextAsync(new BrowserNewContextOptions
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        Locale = locale,
    });

    // Creates Sam's deck with the given title, runs the steps against it as an agent and returns the deck's id.
    public static async Task<string> CreateDeckAsync(string title, Func<Agent, string, Task> steps)
    {
        await using var client = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(client, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
        var deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
        await steps(new Agent(client), deckId);
        return deckId;
    }

    // The agent's calls the journeys use.
    public sealed class Agent(ModelContextProtocol.Client.McpClient client)
    {
        public Task AddSlideAsync(string deckId, string layout, object slots) =>
            AgentSeed.CallAsync(client, "add_slide", new() { ["deck"] = deckId, ["layout"] = layout, ["slots"] = slots });

        // Turns translations on with English as the primary language and Thai offered.
        public Task OfferThaiAsync(string deckId) =>
            AgentSeed.CallAsync(client, "set_translations", new() { ["deck"] = deckId, ["enabled"] = true, ["primary"] = "en", ["languages"] = new[] { "th" } });

        // Writes Thai lines (slot path to text) for one slide, at the revision get_deck shows for it now.
        public async Task TranslateToThaiAsync(string deckId, int slide, Dictionary<string, string> lines)
        {
            var deck = await AgentSeed.CallAsync(client, "get_deck", new() { ["deck"] = deckId });
            var revision = deck.GetProperty("slides").EnumerateArray().ElementAt(slide - 1).GetProperty("revision").GetInt64();
            await AgentSeed.CallAsync(client, "translate_slides", new()
            {
                ["deck"] = deckId,
                ["language"] = "th",
                ["slides"] = new[] { new Dictionary<string, object> { ["slide"] = slide, ["revision"] = revision, ["lines"] = lines } },
            });
        }
    }
}
