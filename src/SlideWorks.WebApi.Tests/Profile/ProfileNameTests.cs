using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Identity.Google;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Profile;

// PUT /api/me/profile (C3): name and colour change together or not at all.
[TestFixture]
public class ProfileNameTests : ApiTestFixture
{
    private const string PROFILE = "/api/me/profile";

    [Test]
    public async Task PutProfile_WhenValidName_ShouldChangeMeAndAdminList()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);
        using var admin = ClientFor(TestUser.Ada);

        var response = await client.PutAsJsonAsync(PROFILE, new { name = "  Quinn Ames  ", colour = "teal" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var identity = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("identity");
        identity.GetProperty("name").GetString().ShouldBe("Quinn Ames");
        identity.GetProperty("colour").GetString().ShouldBe("teal");
        identity.GetProperty("avatar").GetProperty("initials").GetString().ShouldBe("QA");
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        me.GetProperty("name").GetString().ShouldBe("Quinn Ames");
        me.GetProperty("avatar").GetProperty("colour").GetString().ShouldBe("teal");
        var row = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/users")).GetProperty("users").EnumerateArray()
            .Single(u => u.GetProperty("email").GetString() == person.Email);
        row.GetProperty("name").GetString().ShouldBe("Quinn Ames");
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task PutProfile_WhenEmptyOrTooLong_ShouldReturn400AndKeepName(string badName)
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);
        var tooLong = new string('x', 61);

        var empty = await client.PutAsJsonAsync(PROFILE, new { name = badName, colour = "teal" });
        var long61 = await client.PutAsJsonAsync(PROFILE, new { name = tooLong, colour = "teal" });
        var exactly60 = await client.PutAsJsonAsync(PROFILE, new { name = new string('y', 60), colour = "teal" });

        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await empty.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Your name must be 1 to 60 characters.");
        long61.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exactly60.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("name").GetString().ShouldBe(new string('y', 60));
    }

    [Test]
    public async Task PutProfile_WhenRefused_ShouldKeepNameAndColour()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);

        var response = await client.PutAsJsonAsync(PROFILE, new { name = "", colour = "teal" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        me.GetProperty("name").GetString().ShouldBe(person.Name);
        (await client.GetFromJsonAsync<JsonElement>(PROFILE)).GetProperty("identity").GetProperty("colour").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task PutProfile_WhenUnknownColour_ShouldReturn400()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);

        var response = await client.PutAsJsonAsync(PROFILE, new { name = "Quinn Ames", colour = "chartreuse" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("That colour is not one of the choices.");
    }

    [Test]
    public async Task PutProfile_WhenValidNameAndUnknownColour_ShouldReturn400AndKeepName()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);

        var response = await client.PutAsJsonAsync(PROFILE, new { name = "Quinn Ames", colour = "chartreuse" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("name").GetString().ShouldBe(person.Name);
    }

    [Test]
    public async Task PutProfile_WhenNameChanged_ShouldShowInNextActivityEntry()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);
        var deck = await SharingApi.CreateDeckAsync(client);
        await SharingApi.PutOkAsync(client, deck, "link");
        var before = await DeckSeed.ActivityAsync(client, deck);

        (await client.PutAsJsonAsync(PROFILE, new { name = "Quinn Ames", colour = "pink" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await SharingApi.PutOkAsync(client, deck, "private");
        var after = await DeckSeed.ActivityAsync(client, deck);

        after.Count.ShouldBe(before.Count + 1);
        after[0].GetProperty("actor").GetString().ShouldBe("Quinn Ames");
        after.Skip(1).Select(entry => entry.GetProperty("actor").GetString()).ShouldBe(before.Select(entry => entry.GetProperty("actor").GetString()));
        before.ShouldAllBe(entry => entry.GetProperty("actor").GetString() == person.Name);
    }
}

// A Google sign-in after the person chose a name leaves the name alone (D2).
[TestFixture]
public class ProfileSignInTests : GoogleSignInFixture
{
    [Test]
    public async Task SignIn_WhenNameSetByPerson_ShouldKeepName()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);
        (await client.PutAsJsonAsync("/api/me/profile", new { name = "Quinn Ames", colour = "teal" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var browser = Browser();

        using var response = await SignInAsync(browser, person.GoogleSubject, person.Email, name: "Google Renamed");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var me = await MeAsync(browser);
        me.GetProperty("name").GetString().ShouldBe("Quinn Ames");
        me.GetProperty("avatar").GetProperty("colour").GetString().ShouldBe("teal");
    }

    [Test]
    public async Task SignIn_WhenNameNotSetByPerson_ShouldTakeGoogleName()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);
        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var browser = Browser();

        using var response = await SignInAsync(browser, person.GoogleSubject, person.Email, name: "Google Renamed");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await MeAsync(browser)).GetProperty("name").GetString().ShouldBe("Google Renamed");
    }
}
