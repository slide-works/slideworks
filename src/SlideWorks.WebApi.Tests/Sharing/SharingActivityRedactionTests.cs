using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// D35: the change log names who was added to or removed from a deck's list only for the owner and the owner's agent. A person
// with write access reads "changed sharing" on the deck page and in lastChange; the library's "Shared with you" rows show the same
// redacted line, and a person with read access sees no emails there either. Sam owns the deck, Alex has write, or read in the reader case.
[TestFixture]
[CancelAfter(60000)]
public class SharingActivityRedactionTests : ApiTestFixture
{
    [Test]
    public async Task Activity_WhenOwnerReads_ShouldNameThePeople()
    {
        var deck = await ShareWithAlexAsync();

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        SharingSummaries(page).ShouldAllBe(summary => summary.Contains(TestUser.Alex.Email));
        SharingSummaries(page).ShouldNotBeEmpty();
    }

    [Test]
    public async Task Activity_WhenWriterReads_ShouldShowNoEmails()
    {
        var deck = await ShareWithAlexAsync();
        using var alex = ClientFor(TestUser.Alex);

        var page = await alex.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        page.GetProperty("activity").EnumerateArray().Select(row => row.GetProperty("summary").GetString()).ShouldContain("changed sharing");
        page.GetRawText().ShouldNotContain(TestUser.Alex.Email);
    }

    [Test]
    public async Task LastChange_WhenWriterOpensLibrary_ShouldBeRedactedAndOwnersStaysFull()
    {
        var deck = await ShareWithAlexAsync();
        using var alex = ClientFor(TestUser.Alex);

        var shared = (await alex.GetFromJsonAsync<JsonElement>("/api/decks")).GetProperty("sharedWithMe").EnumerateArray().Single(row => row.GetProperty("id").GetString() == deck);
        var own = (await Client.GetFromJsonAsync<JsonElement>("/api/decks")).GetProperty("decks").EnumerateArray().Single(row => row.GetProperty("id").GetString() == deck);

        shared.GetProperty("lastChange").GetProperty("summary").GetString().ShouldBe("changed sharing");
        own.GetProperty("lastChange").GetProperty("summary").GetString()!.ShouldContain(TestUser.Alex.Email);
    }

    [Test]
    public async Task LastChange_WhenReaderOpensLibrary_ShouldShowNoEmailsAndOwnerSeesThem()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var response = await SharingApi.PutAsync(Client, deck, new { level = "named", people = new[] { new { email = TestUser.Alex.Email, access = "read" } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var alex = ClientFor(TestUser.Alex);

        var shared = (await alex.GetFromJsonAsync<JsonElement>("/api/decks")).GetProperty("sharedWithMe").EnumerateArray().Single(row => row.GetProperty("id").GetString() == deck);
        var own = (await Client.GetFromJsonAsync<JsonElement>("/api/decks")).GetProperty("decks").EnumerateArray().Single(row => row.GetProperty("id").GetString() == deck);

        shared.GetProperty("lastChange").GetRawText().ShouldNotContain(TestUser.Alex.Email);
        own.GetProperty("lastChange").GetProperty("summary").GetString()!.ShouldContain(TestUser.Alex.Email);
    }

    private async Task<string> ShareWithAlexAsync()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var response = await SharingApi.PutAsync(Client, deck, new { level = "named", people = new[] { new { email = TestUser.Alex.Email, access = "write" } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return deck;
    }

    private static string[] SharingSummaries(JsonElement page)
        => [.. page.GetProperty("activity").EnumerateArray().Select(row => row.GetProperty("summary").GetString()!).Where(summary => summary.StartsWith("changed sharing", StringComparison.Ordinal))];
}
