using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Slides;

// A video slide stores the YouTube link it was given. A link that is not one YouTube video is refused
// before anything is written, the same way a javascript link is refused.
[TestFixture]
[CancelAfter(30000)]
public class VideoSlideTests : ApiTestFixture
{
    private const string GOOD = "https://www.youtube.com/watch?v=M7lc1UVf-VE&list=PLkeep-this";

    [Test]
    public async Task AddSlide_WhenTheUrlIsYouTube_ShouldStoreItUnchanged()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        await DeckSeed.AddSlideAsync(agent, deck, "video", Slots(GOOD));

        var slide = (await GetDeckAsync(agent, deck)).GetProperty("slides")[0];
        slide.GetProperty("layout").GetString().ShouldBe("video");
        slide.GetProperty("slots").GetProperty("url").GetString().ShouldBe(GOOD);
    }

    [Test]
    public async Task AddSlide_WhenTheUrlIsNotYouTube_ShouldRefuseAndAddNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        using var slots = JsonDocument.Parse(Slots("https://example.com/watch?v=M7lc1UVf-VE"));
        var (isError, json) = await agent.CallToolAsync("add_slide", new { deck, layout = "video", slots = slots.RootElement });

        isError.ShouldBeTrue(json.ToString());
        json.GetString()!.ShouldStartWith("video.url: ");
        (await GetDeckAsync(agent, deck)).GetProperty("slides").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task AddSlide_WhenTheUrlIsJavascript_ShouldRefuseAndAddNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        using var slots = JsonDocument.Parse(Slots("javascript:alert(1)"));
        var (isError, json) = await agent.CallToolAsync("add_slide", new { deck, layout = "video", slots = slots.RootElement });

        isError.ShouldBeTrue(json.ToString());
        json.GetString()!.ShouldStartWith("video.url: ");
        (await GetDeckAsync(agent, deck)).GetProperty("slides").GetArrayLength().ShouldBe(0);
    }

    private static string Slots(string url) =>
        $$"""{ "headline": "Watch the slice ship", "url": "{{url}}" }""";

    private static async Task<JsonElement> GetDeckAsync(McpTestClient agent, string deck)
    {
        var (isError, json) = await agent.CallToolAsync("get_deck", new { deck });
        isError.ShouldBeFalse(json.ToString());
        return json;
    }
}
