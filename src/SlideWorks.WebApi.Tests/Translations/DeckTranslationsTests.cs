using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Translations;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K2: the deck's translation settings read, and the rows TranslationRowSeed writes.
[TestFixture]
public class DeckTranslationsTests : ApiTestFixture
{
    [Test]
    public async Task ForDeck_WhenNoRow_ShouldBeOffWithEnglishPrimary()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await using var scope = Factory.Services.CreateAsyncScope();

        var settings = await scope.ServiceProvider.GetRequiredService<IDeckTranslations>().ForDeckAsync(deck);

        settings.Enabled.ShouldBeFalse();
        settings.Primary.ShouldBe("en");
        settings.Languages.ShouldBeEmpty();
    }

    [Test]
    public async Task ForDeck_WhenSeeded_ShouldReturnLanguagesInOrder()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await new TranslationRowSeed(Factory.Services).SettingsAsync(deck, true, "en", "th", "ja", "es");
        await using var scope = Factory.Services.CreateAsyncScope();

        var settings = await scope.ServiceProvider.GetRequiredService<IDeckTranslations>().ForDeckAsync(deck);

        settings.Enabled.ShouldBeTrue();
        settings.Primary.ShouldBe("en");
        settings.Languages.ShouldBe(["th", "ja", "es"]);
    }

    [Test]
    public async Task LinesAsync_WhenSeeded_ShouldStoreTheK2Shape()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Hello");
        var lines = new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            ["th"] = new Dictionary<string, (string Text, string From)> { ["/title"] = ("สวัสดี", "Hello") },
        };

        await new TranslationRowSeed(Factory.Services).LinesAsync(deck, 1, lines);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var json = await db.Slides.AsNoTracking().Where(s => s.DeckId == deck && s.Position == 0).Select(s => s.TranslationsJson).SingleAsync();
        var entry = JsonDocument.Parse(json!).RootElement.GetProperty("th").GetProperty("/title");
        entry.GetProperty("text").GetString().ShouldBe("สวัสดี");
        entry.GetProperty("from").GetString().ShouldBe("Hello");
    }
}
