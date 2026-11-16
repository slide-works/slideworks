using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// GET /d/{id}/{slug?} (L9, D12): the SPA's index.html with Open Graph and Twitter card tags. A link-shared deck names
// its title, subtitle, slide count and author; an unknown, private or named-viewer deck gets the generic tags. Every
// value is HTML-encoded. CI's backend job has no built wwwroot, so the host serves a stand-in index.html.
[TestFixture]
public partial class OpenGraphTests : ApiTestFixture
{
    private const string INDEX = "<!doctype html><html><head><title>SlideWorks</title></head><body><div id=\"root\"></div></body></html>";

    private readonly string _webRoot = CreateWebRoot();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.Configure<StaticFileOptions>(options => options.FileProvider = new PhysicalFileProvider(_webRoot));

    [OneTimeTearDown]
    public void DeleteWebRoot() => Directory.Delete(_webRoot, recursive: true);

    [Test]
    public async Task Get_WhenLinkDeck_ShouldNameTheDeckInTheTags()
    {
        var deck = await CreateDeckAsync("Ship smaller", "link");

        var (html, tags) = await PageAsync($"/d/{deck}/ship-smaller");

        tags["og:title"].ShouldBe("Ship smaller");
        tags["og:description"].ShouldBe("Why small batches win · 1 slide · Sam Rivera");
        tags["og:image"].ShouldEndWith($"/api/cards/{deck}.png");
        tags["og:image"].ShouldStartWith("http");
        tags["og:url"].ShouldEndWith($"/d/{deck}/ship-smaller");
        tags["twitter:card"].ShouldBe("summary_large_image");
        html.ShouldContain("<div id=\"root\"></div>");
        html.IndexOf("og:title", StringComparison.Ordinal).ShouldBeLessThan(html.IndexOf("</head>", StringComparison.Ordinal));
    }

    [TestCase("private")]
    [TestCase("named")]
    public async Task Get_WhenDeckIsNotLinkShared_ShouldReturnGenericTags(string sharing)
    {
        var deck = await CreateDeckAsync("Secret title", sharing);

        var (html, tags) = await PageAsync($"/d/{deck}/secret-title");

        tags["og:title"].ShouldBe("SlideWorks");
        tags["og:image"].ShouldEndWith($"/api/cards/{deck}.png");
        tags["twitter:card"].ShouldBe("summary_large_image");
        tags.ShouldNotContainKey("og:url");
        html.ShouldNotContain("Secret title");
        html.ShouldNotContain("Sam Rivera");
        html.ShouldNotContain("Why small batches win");
    }

    [Test]
    public async Task Get_WhenUnknownDeckWithNoSlug_ShouldReturn200WithGenericTags()
    {
        var (_, tags) = await PageAsync("/d/no-such-deck");

        tags["og:title"].ShouldBe("SlideWorks");
        tags["og:image"].ShouldEndWith("/api/cards/no-such-deck.png");
    }

    [Test]
    public async Task Get_WhenTitleHoldsMarkup_ShouldEncodeIt()
    {
        var deck = await CreateDeckAsync("<script>alert(\"x\")</script> & co", "link");

        var (html, tags) = await PageAsync($"/d/{deck}/x");

        html.ShouldNotContain("<script>alert");
        html.ShouldContain("&lt;script&gt;");
        tags["og:title"].ShouldBe("<script>alert(\"x\")</script> & co");
    }

    private async Task<(string Html, Dictionary<string, string> Tags)> PageAsync(string path)
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        var response = await anonymous.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, html);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        var tags = MetaTag().Matches(html).ToDictionary(m => m.Groups["key"].Value, m => WebUtility.HtmlDecode(m.Groups["value"].Value));
        return (html, tags);
    }

    private async Task<string> CreateDeckAsync(string title, string sharing)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var (isError, created) = await agent.CallToolAsync("create_deck", new { title, subtitle = "Why small batches win", author = "Sam Rivera" });
        isError.ShouldBeFalse(created.ToString());
        var deck = created.GetProperty("deck").GetString()!;
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "One", "items": ["a"] }""");
        if (sharing != "private")
        {
            var viewers = sharing == "named" ? new[] { "viewer@example.com" } : [];
            (await Client.PutAsJsonAsync($"/api/decks/{deck}/sharing", new { level = sharing, viewers })).EnsureSuccessStatusCode();
        }
        return deck;
    }

    private static string CreateWebRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slide-works-og-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "index.html"), INDEX);
        return root;
    }

    [GeneratedRegex("<meta (?:property|name)=\"(?<key>[^\"]+)\" content=\"(?<value>[^\"]*)\"")]
    private static partial Regex MetaTag();
}
