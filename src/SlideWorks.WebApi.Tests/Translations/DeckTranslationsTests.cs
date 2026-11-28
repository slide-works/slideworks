using Microsoft.Extensions.DependencyInjection;
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
}
