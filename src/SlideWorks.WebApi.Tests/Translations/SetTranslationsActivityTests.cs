using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.Timing;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K4 set_translations: one actor-first activity entry naming each changed part in order, DeckChanged on a change,
// and nothing recorded or published when the call changes nothing.
[TestFixture]
[CancelAfter(60000)]
public class SetTranslationsActivityTests : ApiTestFixture
{
    [Test]
    public async Task SetTranslations_WhenTurnedOn_ShouldRecordActivity()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var events = DeckEventsUntilMarker.Subscribe(Factory.Services.GetRequiredService<IDeckEvents>(), deck);

        await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = true, primary = "en", languages = new[] { "th", "ja" } });

        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];
        entry.GetProperty("summary").GetString().ShouldBe("turned translations on, with English as the primary language, with Thai and Japanese");
        entry.GetProperty("tool").GetString().ShouldBe("set_translations");
        entry.GetProperty("via").GetString().ShouldBe("mcp");
        entry.GetProperty("actor").GetString().ShouldBe("Claude");
        (await events.BeforeMarkerAsync()).ShouldHaveSingleItem().ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
    }

    [Test]
    public async Task SetTranslations_WhenOnlyListChanges_ShouldNameOnlyTheList()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = true });

        await SlideCalls.OkAsync(agent, "set_translations", new { deck, languages = new[] { "th" } });
        await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = false });

        (await DeckSeed.ActivityAsync(Client, deck)).Select(e => e.GetProperty("summary").GetString()).Take(3)
            .ShouldBe(["turned translations off", "with Thai", "turned translations on, with English as the primary language, with no other languages yet"]);
    }

    [Test]
    public async Task SetTranslations_WhenNothingChanged_ShouldRecordNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = true, languages = new[] { "th" } });
        var recorded = (await DeckSeed.ActivityAsync(Client, deck)).Count;
        using var events = DeckEventsUntilMarker.Subscribe(Factory.Services.GetRequiredService<IDeckEvents>(), deck);

        await SlideCalls.OkAsync(agent, "set_translations", new { deck, enabled = true, primary = "en", languages = new[] { "th" } });

        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(recorded);
        (await events.BeforeMarkerAsync()).ShouldBeEmpty();
    }
}
