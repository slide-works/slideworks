using System.Net;
using System.Text.RegularExpressions;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-2's share-card journey (L9, D12) through the real renderer: an agent makes two decks with the same look, one shared
// by link and one private, and a link-preview bot (anonymous, no cookie) reads each one's /d/{id} tags and og:image.
// The link deck's card and tags carry its title; the private deck's are the generic ones, whatever its title. A card
// carries a title when two decks that differ only in title get different PNGs, and a private deck's PNG is byte for byte
// the generic card's.
[TestFixture]
public partial class Sw2ShareCardJourneyTests
{
    private const string PALETTE = "ember";

    [Test]
    public async Task LinkDeckShareCard_HasTitle_AndPrivateDeckDoesNot()
    {
        var link = await CreateDeckAsync("Ship smaller every week", "link");
        var otherLink = await CreateDeckAsync("Measure twice, cut once", "link");
        var secret = await CreateDeckAsync("Quarterly hiring plan", "private");
        var otherSecret = await CreateDeckAsync("Another secret plan", "private");
        using var bot = new HttpClient { BaseAddress = new Uri(AcceptanceApp.BaseUrl) };

        var linkCard = await CardAsync(bot, link);
        var otherLinkCard = await CardAsync(bot, otherLink);
        var secretCard = await CardAsync(bot, secret);
        var otherSecretCard = await CardAsync(bot, otherSecret);
        var genericCard = await CardAsync(bot, "nosuchdeck01");

        linkCard.ShouldNotBe(otherLinkCard, "a link deck's card should change with its title");
        linkCard.ShouldNotBe(genericCard, "a link deck should get its own card");
        secretCard.ShouldBe(genericCard, "a private deck should get the generic card");
        otherSecretCard.ShouldBe(genericCard, "a private deck's title should not reach its card");

        var linkTags = await TagsAsync(bot, link);
        linkTags["og:title"].ShouldBe("Ship smaller every week");
        linkTags["og:image"].ShouldBe($"{AcceptanceApp.BaseUrl}/api/cards/{link}.png");

        var (secretTags, secretHtml) = await PageAsync(bot, secret);
        secretTags["og:title"].ShouldBe(secretTags["og:site_name"], "a private deck's tags should name the site, not the deck");
        secretTags["og:image"].ShouldBe($"{AcceptanceApp.BaseUrl}/api/cards/{secret}.png");
        secretHtml.ShouldNotContain("Quarterly hiring plan");
        secretHtml.ShouldNotContain(TestUser.Sam.Name);
    }

    private static async Task<string> CreateDeckAsync(string title, string sharing)
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = title, ["palette"] = PALETTE });
        var deck = created.GetProperty("deck").GetString()!;
        if (sharing != "private")
        {
            await AgentSeed.CallAsync(agent, "set_sharing", new() { ["deck"] = deck, ["level"] = sharing });
        }
        return deck;
    }

    // The card a link preview fetches: a 1200x630 PNG, anonymous, with no cookie.
    private static async Task<byte[]> CardAsync(HttpClient bot, string deck)
    {
        using var response = await bot.GetAsync($"/api/cards/{deck}.png");
        var body = await response.Content.ReadAsByteArrayAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"card for {deck}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("image/png");
        body.AsSpan(0, 8).ToArray().ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        AgentSeed.PngSize(body).ShouldBe((1200, 630));
        return body;
    }

    private static async Task<Dictionary<string, string>> TagsAsync(HttpClient bot, string deck) => (await PageAsync(bot, deck)).Tags;

    private static async Task<(Dictionary<string, string> Tags, string Html)> PageAsync(HttpClient bot, string deck)
    {
        using var response = await bot.GetAsync($"/d/{deck}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"/d/{deck}");
        var tags = OpenGraph().Matches(html).ToDictionary(m => m.Groups["key"].Value, m => WebUtility.HtmlDecode(m.Groups["value"].Value));
        return (tags, html);
    }

    [GeneratedRegex("<meta property=\"(?<key>og:[a-z_:]+)\" content=\"(?<value>[^\"]*)\"")]
    private static partial Regex OpenGraph();
}
