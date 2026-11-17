using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Sharing;

namespace SlideWorks.WebApi.Tests.Look;

// chrome.joinBar on PUT /api/decks/{id}/look (J6, D5): off by default, a plain boolean that merges with the other chrome
// overrides, and the look every viewer reads carries it.
[TestFixture]
[CancelAfter(30000)]
public class JoinBarLookTests : ApiTestFixture
{
    [Test]
    public async Task Put_WhenJoinBarSet_ShouldRoundTrip()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        (await PlayJoinBarAsync(deck)).ShouldBeFalse();

        var on = await PutAsync(deck, """{ "chrome": { "joinBar": true } }""");
        var kept = await PutAsync(deck, """{ "chrome": { "tracker": false } }""");
        var play = await PlayJoinBarAsync(deck);
        var off = await PutAsync(deck, """{ "chrome": { "joinBar": false } }""");

        on.GetProperty("look").GetProperty("joinBar").GetBoolean().ShouldBeTrue();
        on.GetProperty("look").GetProperty("chromeOverrides").GetProperty("joinBar").GetBoolean().ShouldBeTrue();
        kept.GetProperty("look").GetProperty("joinBar").GetBoolean().ShouldBeTrue();
        kept.GetProperty("look").GetProperty("chromeOverrides").GetProperty("tracker").GetBoolean().ShouldBeFalse();
        play.ShouldBeTrue();
        off.GetProperty("look").GetProperty("joinBar").GetBoolean().ShouldBeFalse();
        (await PlayJoinBarAsync(deck)).ShouldBeFalse();
    }

    [TestCase("null")]
    [TestCase("\"yes\"")]
    [TestCase("1")]
    public async Task Put_WhenJoinBarNotABoolean_ShouldReturn400(string value)
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        using var response = await Client.PutAsync($"/api/decks/{deck}/look", new StringContent($$"""{ "chrome": { "joinBar": {{value}} } }""", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"chrome.joinBar: must be true or false"}""");
    }

    private async Task<JsonElement> PutAsync(string deck, string body)
    {
        using var response = await Client.PutAsync($"/api/decks/{deck}/look", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private async Task<bool> PlayJoinBarAsync(string deck)
        => (await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}")).GetProperty("look").GetProperty("joinBar").GetBoolean();
}
