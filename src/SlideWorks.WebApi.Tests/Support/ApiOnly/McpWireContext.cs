using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;

namespace SlideWorks.WebApi.Tests.Support.ApiOnly;

// The state every wire case draws on, made through the public tools and routes only: a deck of five slides (1 quote,
// 2 poll, 3 word cloud, 4 open text, 5 Q&A) taken live by its owner, and a draft template.
public sealed class McpWireContext
{
    public const int POLL_SLIDE = 2;
    public const int WORD_CLOUD_SLIDE = 3;
    public const int OPEN_TEXT_SLIDE = 4;

    private readonly McpTestClient _agent;
    private readonly WebApplicationFactory<ApiMarker> _factory;

    private McpWireContext(McpTestClient agent, WebApplicationFactory<ApiMarker> factory, string deck, string template)
    {
        _agent = agent;
        _factory = factory;
        Deck = deck;
        Template = template;
    }

    public string Deck { get; }

    public string Template { get; }

    public static async Task<McpWireContext> SeedAsync(WebApplicationFactory<ApiMarker> factory, HttpClient owner, McpTestClient agent)
    {
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Small batches ship sooner");
        await DeckSeed.AddSlideAsync(agent, deck, "poll", """{"question":"How big was your last merge request?","options":["Under 50 lines","50 to 200 lines","Over 200 lines"]}""");
        await DeckSeed.AddSlideAsync(agent, deck, "word-cloud", """{"question":"How do you feel?"}""");
        await DeckSeed.AddSlideAsync(agent, deck, "open-text", """{"question":"What would help your team ship smaller changes?"}""");
        await DeckSeed.AddSlideAsync(agent, deck, "qa", "{}");
        using var live = await owner.PostAsync($"/api/decks/{deck}/live", null);
        live.EnsureSuccessStatusCode();

        var (isError, template) = await agent.CallToolAsync("create_template", new { name = "Kickoff", from = "workshop" });
        isError.ShouldBeFalse(template.ToString());
        return new McpWireContext(agent, factory, deck, template.GetProperty("template").GetString()!);
    }

    // Runs another tool first, as an agent would before the tool under test; fails the case if that tool errors.
    public async Task<JsonElement> SetUpAsync(string tool, object args)
    {
        var (isError, json) = await _agent.CallToolAsync(tool, args);
        isError.ShouldBeFalse($"{tool} (set-up): {json}");
        return json;
    }

    // Turns translations on in these languages with TranslationRowSeed, so a tool under test does not depend on set_translations.
    public Task TranslationsOnAsync(params string[] languages)
        => new TranslationRowSeed(_factory.Services).SettingsAsync(Deck, true, "en", languages);

    public async Task<long> RevisionOfSlideAsync(int slide)
    {
        var deck = await SetUpAsync("get_deck", new { deck = Deck });
        return deck.GetProperty("slides").EnumerateArray().Single(s => s.GetProperty("number").GetInt32() == slide).GetProperty("revision").GetInt64();
    }

    public async Task<JsonElement> CallOnWireAsync(string tool, string argumentsJson)
        => await McpWire.CallToolAsync(_factory, _agent.Tokens.AccessToken, tool, argumentsJson);
}
