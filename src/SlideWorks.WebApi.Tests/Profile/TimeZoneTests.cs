using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.Core;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Identity.Google;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Profile;

// PUT /api/me/time-zone (C3) and the labels that follow the saved zone (C7, D17). Labels are checked at 20:00 UTC, when
// Bangkok (+07) is already on the next calendar day, so a label in the wrong zone shows the wrong date.
[TestFixture]
public class TimeZoneTests : ApiTestFixture
{
    private const string ROUTE = "/api/me/time-zone";
    private const string BANGKOK = "Asia/Bangkok";

    [Test]
    public async Task PutTimeZone_WhenKnown_ShouldFormatOldDatesInThatZone()
    {
        var bangkok = TestUser.NewApproved();
        var utc = TestUser.NewApproved();
        using var bangkokClient = ClientFor(bangkok);
        using var utcClient = ClientFor(utc);
        (await bangkokClient.PutAsJsonAsync(ROUTE, new { timeZone = BANGKOK, fromBrowser = false })).StatusCode.ShouldBe(HttpStatusCode.OK);
        AdvanceToTwentyHundredUtc();
        var created = Clock.GetUtcNow();
        var bangkokDeck = await CreateDeckAsync(bangkok);
        var utcDeck = await CreateDeckAsync(utc);
        Clock.Advance(TimeSpan.FromDays(10));

        var bangkokLabel = (await DeckSeed.ActivityAsync(bangkokClient, bangkokDeck))[0].GetProperty("atLabel").GetString();
        var utcLabel = (await DeckSeed.ActivityAsync(utcClient, utcDeck))[0].GetProperty("atLabel").GetString();

        bangkokLabel.ShouldBe(created.AddHours(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        utcLabel.ShouldBe(created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        bangkokLabel.ShouldNotBe(utcLabel);
    }

    [Test]
    public async Task PutTimeZone_WhenKnown_ShouldFormatAdminListInThatZone()
    {
        using var admin = ClientFor(TestUser.Ada);
        (await admin.PutAsJsonAsync(ROUTE, new { timeZone = BANGKOK, fromBrowser = false })).StatusCode.ShouldBe(HttpStatusCode.OK);
        AdvanceToTwentyHundredUtc();
        var created = Clock.GetUtcNow();
        var pending = TestUser.NewPending();
        using (var scope = Factory.Services.CreateScope())
        {
            await TestPeople.SignInAsync(scope.ServiceProvider, pending);
        }
        Clock.Advance(TimeSpan.FromDays(10));

        var users = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/users")).GetProperty("users");

        var row = users.EnumerateArray().Single(u => u.GetProperty("email").GetString() == pending.Email);
        row.GetProperty("requestedLabel").GetString().ShouldBe(created.AddHours(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task PutTimeZone_WhenKnown_ShouldKeepSlideFooterAndTemplateLabelsInUtc()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);
        (await client.PutAsJsonAsync(ROUTE, new { timeZone = BANGKOK, fromBrowser = false })).StatusCode.ShouldBe(HttpStatusCode.OK);
        AdvanceToTwentyHundredUtc();
        var created = Clock.GetUtcNow();
        var deck = await CreateDeckAsync(person);
        var template = await client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Zoned" });
        template.StatusCode.ShouldBe(HttpStatusCode.Created, await template.Content.ReadAsStringAsync());
        var templateId = (await template.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        Clock.Advance(TimeSpan.FromDays(10));

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");
        var list = await client.GetFromJsonAsync<JsonElement>("/api/templates");

        page.GetProperty("look").GetProperty("footerDateLabel").GetString().ShouldBe(DisplayDate.Day(created));
        var row = list.GetProperty("mine").EnumerateArray().Single(r => r.GetProperty("id").GetString() == templateId);
        row.GetProperty("updatedLabel").GetString().ShouldBe(created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task PutTimeZone_WhenNoneSaved_ShouldFormatInUtc()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);
        AdvanceToTwentyHundredUtc();
        var created = Clock.GetUtcNow();
        var deck = await CreateDeckAsync(person);
        Clock.Advance(TimeSpan.FromDays(10));

        var label = (await DeckSeed.ActivityAsync(client, deck))[0].GetProperty("atLabel").GetString();

        label.ShouldBe(created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task PutTimeZone_WhenKnown_ShouldSaveItAndShowItOnMeAndProfile()
    {
        using var client = ClientFor(TestUser.NewApproved());

        var response = await client.PutAsJsonAsync(ROUTE, new { timeZone = BANGKOK, fromBrowser = false });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("account").GetProperty("timeZone").GetString().ShouldBe(BANGKOK);
        (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("timeZone").GetString().ShouldBe(BANGKOK);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task PutTimeZone_WhenUnknown_ShouldReturn400(bool fromBrowser, bool zoneAlreadySet)
    {
        using var client = ClientFor(TestUser.NewApproved());
        if (zoneAlreadySet)
        {
            await client.PutAsJsonAsync(ROUTE, new { timeZone = BANGKOK, fromBrowser = false });
        }

        var response = await client.PutAsJsonAsync(ROUTE, new { timeZone = "Mars/Olympus_Mons", fromBrowser });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldNotBeNullOrEmpty();
        var saved = (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("timeZone");
        (saved.ValueKind == JsonValueKind.Null ? null : saved.GetString()).ShouldBe(zoneAlreadySet ? BANGKOK : null);
    }

    [Test]
    public async Task PutTimeZone_WhenFromBrowserAndAlreadySet_ShouldKeepZone()
    {
        using var client = ClientFor(TestUser.NewApproved());
        await client.PutAsJsonAsync(ROUTE, new { timeZone = BANGKOK, fromBrowser = false });

        var response = await client.PutAsJsonAsync(ROUTE, new { timeZone = "Europe/London", fromBrowser = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("account").GetProperty("timeZone").GetString().ShouldBe(BANGKOK);
    }

    // Pins the no-op: with a zone already set, fromBrowser changes nothing and is not checked, so even an id this server
    // does not know answers 200 (C3).
    [Test]
    public async Task PutTimeZone_WhenFromBrowserUnknownAndAlreadySet_ShouldKeepZoneWith200()
    {
        using var client = ClientFor(TestUser.NewApproved());
        await client.PutAsJsonAsync(ROUTE, new { timeZone = BANGKOK, fromBrowser = false });

        var response = await client.PutAsJsonAsync(ROUTE, new { timeZone = "Mars/Olympus_Mons", fromBrowser = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("timeZone").GetString().ShouldBe(BANGKOK);
    }

    [Test]
    public async Task PutTimeZone_WhenFromBrowserAndEmpty_ShouldSetZone()
    {
        using var client = ClientFor(TestUser.NewApproved());

        var response = await client.PutAsJsonAsync(ROUTE, new { timeZone = "Europe/London", fromBrowser = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("timeZone").GetString().ShouldBe("Europe/London");
    }

    private void AdvanceToTwentyHundredUtc()
    {
        var now = Clock.GetUtcNow();
        var target = now.Date.AddHours(20);
        Clock.Advance(target > now ? target - now : target.AddDays(1) - now);
    }

    private async Task<string> CreateDeckAsync(TestUser person)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, person);
        return await DeckSeed.CreateDeckAsync(agent);
    }
}
