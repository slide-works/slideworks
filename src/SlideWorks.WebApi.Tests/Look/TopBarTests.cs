using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// D13: chrome.topBar turns the template's accent bar on or off for one deck, and null goes back to the template's own.
[TestFixture]
public class TopBarTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    private FakeTemplates Templates => Factory.Services.GetRequiredService<FakeTemplates>();

    [Test]
    public async Task SetLook_WhenTopBarIsAccent_ShouldCarryItToThePlayView()
    {
        var deck = await CreateDeckAsync("tpl_topbaraccent1", "none");

        var result = await PutAsync(deck, """{"chrome":{"topBar":"accent"}}""");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PlayLookAsync(deck)).GetProperty("topBar").GetString().ShouldBe("accent");
        (await PageAsync(deck)).GetProperty("look").GetProperty("chromeOverrides").GetProperty("topBar").GetString().ShouldBe("accent");
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe("changed the top bar to accent");
    }

    [Test]
    public async Task SetLook_WhenTopBarIsNull_ShouldGoBackToTheTemplatesDefault()
    {
        var deck = await CreateDeckAsync("tpl_topbarnull0001", TemplateLook.ACCENT_TOP_BAR);
        await PutAsync(deck, """{"chrome":{"topBar":"none"}}""");
        (await PlayLookAsync(deck)).GetProperty("topBar").GetString().ShouldBe("none");

        var result = await PutAsync(deck, """{"chrome":{"topBar":null}}""");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PlayLookAsync(deck)).GetProperty("topBar").GetString().ShouldBe("accent");
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe("changed the top bar to the template's default");
    }

    [TestCase("""{"chrome":{"topBar":"wide"}}""")]
    [TestCase("""{"chrome":{"topBar":true}}""")]
    [TestCase("""{"chrome":{"topBar":"ACCENT"}}""")]
    public async Task SetLook_WhenTopBarIsNotAWord_ShouldChangeNothing(string body)
    {
        var deck = await CreateDeckAsync("tpl_topbarbad00001", "none");
        var before = await PageAsync(deck);

        var response = await PutAsync(deck, body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("chrome.topBar: must be 'accent', 'none' or null");
        var after = await PageAsync(deck);
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64());
        after.GetProperty("activity").GetArrayLength().ShouldBe(1);
        (await PlayLookAsync(deck)).GetProperty("topBar").GetString().ShouldBe("none");
    }

    [Test]
    public async Task Play_WhenTheTemplateIsBuiltIn_ShouldSayNone()
    {
        var deck = await CreateDeckAsync(null, "none");

        (await PlayLookAsync(deck)).GetProperty("topBar").GetString().ShouldBe("none");
        await PutAsync(deck, """{"chrome":{"tracker":false}}""");
        (await PlayLookAsync(deck)).GetProperty("topBar").GetString().ShouldBe("none");
    }

    // An uploaded template whose own top bar is the one given, or a built-in when no ref is given.
    private async Task<string> CreateDeckAsync(string? templateRef, string templateTopBar)
    {
        if (templateRef is not null)
        {
            Templates.Add(new ResolvedTemplate(new TemplateCatalogue().Default with { Name = templateRef, Label = "Northwind", IsDefault = false }, templateRef, 1, BuiltIn: false)
            {
                LatestVersion = 1,
                Look = TemplateLook.None with { TopBar = templateTopBar },
            });
        }
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Top bar", template = templateRef });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private Task<HttpResponseMessage> PutAsync(string deck, string body)
        => Client.PutAsync($"/api/decks/{deck}/look", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

    private async Task<JsonElement> PlayLookAsync(string deck) => (await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}")).GetProperty("look");

    private Task<JsonElement> PageAsync(string deck) => Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");
}
