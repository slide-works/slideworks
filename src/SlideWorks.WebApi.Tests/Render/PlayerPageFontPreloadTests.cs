using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// GET /d/{id}/{slug?}: a link-shared deck on a template with uploaded fonts preloads each file, so the first slide does
// not flash. The page is anonymous and a preload url holds the template's id, so any other deck's page, and a built-in
// deck's page, carry none and are byte-for-byte what they were. A stand-in index.html stands for the SPA, as in OpenGraphTests.
[TestFixture]
public class PlayerPageFontPreloadTests : ApiTestFixture
{
    private const string INDEX = "<!doctype html><html><head><title>SlideWorks</title></head><body><div id=\"root\"></div></body></html>";
    private const string TEMPLATE = "tpl_fontsample";
    private const string HEAD_FILE = "ast_head";
    private const string BODY_FILE = "ast_body";

    private readonly string _webRoot = CreateWebRoot();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeTemplates.Register(services);
        services.Configure<StaticFileOptions>(options => options.FileProvider = new PhysicalFileProvider(_webRoot));
    };

    [OneTimeSetUp]
    public void AddTemplate()
    {
        var look = TemplateLook.None with
        {
            Fonts =
            [
                new TemplateFont("Harbour Sans", "head", HEAD_FILE, 700, "normal"),
                new TemplateFont("Harbour Serif", "body", BODY_FILE, 400, "normal"),
            ],
        };
        Factory.Services.GetRequiredService<FakeTemplates>().Add(
            new ResolvedTemplate(new TemplateCatalogue().Default with { Name = TEMPLATE, Label = "Harbour", IsDefault = false }, TEMPLATE, 1, false) { Look = look, LatestVersion = 1 });
    }

    [OneTimeTearDown]
    public void DeleteWebRoot() => Directory.Delete(_webRoot, recursive: true);

    [Test]
    public async Task Page_WhenTheTemplateHasFonts_ShouldPreloadEach()
    {
        var deck = await CreateDeckAsync(TEMPLATE, "link");

        var html = await PageAsync(deck);

        foreach (var file in new[] { HEAD_FILE, BODY_FILE })
        {
            html.ShouldContain($"<link rel=\"preload\" as=\"font\" type=\"font/woff2\" crossorigin href=\"/api/templates/{TEMPLATE}/assets/{file}\" />");
        }
        html.IndexOf("rel=\"preload\"", StringComparison.Ordinal).ShouldBeLessThan(html.IndexOf("</head>", StringComparison.Ordinal));
    }

    [TestCase("private")]
    [TestCase("named")]
    public async Task Page_WhenTheDeckIsNotLinkShared_ShouldPreloadNothing(string sharing)
    {
        var deck = await CreateDeckAsync(TEMPLATE, sharing);

        var html = await PageAsync(deck);

        html.ShouldNotContain("preload");
        html.ShouldNotContain(TEMPLATE);
        html.ShouldNotContain(HEAD_FILE);
    }

    [Test]
    public async Task Page_WhenTheTemplateIsBuiltIn_ShouldBeUnchanged()
    {
        var deck = await CreateDeckAsync(new TemplateCatalogue().Default.Name, "link");

        var html = await PageAsync(deck);

        html.ShouldNotContain("preload");
        html.ShouldNotContain("<link");
    }

    private async Task<string> PageAsync(string deck)
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        var response = await anonymous.GetAsync($"/d/{deck}/x");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, html);
        return html;
    }

    private async Task<string> CreateDeckAsync(string template, string sharing)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var (isError, created) = await agent.CallToolAsync("create_deck", new { title = "Fonts", template });
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
        var root = Path.Combine(Path.GetTempPath(), $"slide-works-fonts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "index.html"), INDEX);
        return root;
    }
}
