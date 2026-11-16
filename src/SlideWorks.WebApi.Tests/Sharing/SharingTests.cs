using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// GET and PUT /api/decks/{id}/sharing (K4 sharing.json) and what each level lets people do with the player
// (GET /api/play/{id}, K8): link opens it to anyone, named to the listed signed-in people (pending ones too, SW-54 D2),
// and private to the owner only. Sam owns every deck here. The fixture's app is shared, so each test makes its own deck.
[TestFixture]
public class SharingTests : ApiTestFixture
{
    [Test]
    public async Task Get_ShouldMatchShape()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await Client.GetAsync($"/api/decks/{deck}/sharing");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var view = JsonDocument.Parse(body).RootElement;
        JsonShape.AssertMatches(view, "sharing.json", ScreenShapes.Sharing);
        view.GetProperty("level").GetString().ShouldBe("private");
        view.GetProperty("viewers").GetArrayLength().ShouldBe(0);
        view.GetProperty("link").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller");
        view.GetProperty("lastChange").ValueKind.ShouldBe(JsonValueKind.Null);
        body.ShouldContain("\"level\":\"private\"", Case.Sensitive);
    }

    [Test]
    public async Task Put_WhenNamed_ShouldReturnNormalizedViewersAndLastChangeMatchingShape()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        Clock.Advance(TimeSpan.FromMinutes(3));

        var response = await SharingApi.PutAsync(Client, deck, new { level = "named", viewers = new[] { "  Mira@Team.Example ", "alex@team.example", "ALEX@team.example" } });
        Clock.Advance(TimeSpan.FromMinutes(2));
        var read = await Client.GetAsync($"/api/decks/{deck}/sharing");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var put = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(put, "sharing.json", ScreenShapes.Sharing);
        put.GetProperty("level").GetString().ShouldBe("named");
        put.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe(["alex@team.example", "mira@team.example"]);
        put.GetProperty("lastChange").GetProperty("atLabel").GetString().ShouldBe("just now");
        var body = await read.Content.ReadAsStringAsync();
        var view = JsonDocument.Parse(body).RootElement;
        JsonShape.AssertMatches(view, "sharing.json", ScreenShapes.Sharing);
        view.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe(["alex@team.example", "mira@team.example"]);
        var lastChange = view.GetProperty("lastChange");
        lastChange.GetProperty("actor").GetString().ShouldBe(TestUser.Sam.Name);
        lastChange.GetProperty("actorKind").GetString().ShouldBe("person");
        lastChange.GetProperty("atLabel").GetString().ShouldBe("2 min ago");
        body.ShouldContain("\"level\":\"named\"", Case.Sensitive);
        body.ShouldContain("\"actorKind\":\"person\"", Case.Sensitive);
    }

    [Test]
    public async Task Put_WhenLink_ShouldLetAnonymousLoadPlayView()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        using var anonymous = ClientFor(TestUser.Anonymous);
        (await anonymous.GetAsync($"/api/play/{deck}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await SharingApi.PutOkAsync(Client, deck, "link");
        var response = await anonymous.GetAsync($"/api/play/{deck}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString().ShouldBe(deck);
    }

    [Test]
    public async Task Put_WhenNamed_ShouldLetListedViewerLoadPlayView()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        using var alex = ClientFor(TestUser.Alex);

        await SharingApi.PutOkAsync(Client, deck, "named", "ALEX@team.example");
        var response = await alex.GetAsync($"/api/play/{deck}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString().ShouldBe(deck);
    }

    // SW-54 D2: naming someone vouches for them, so a person still waiting for approval opens the deck. The sharing
    // endpoints themselves stay approved-only.
    [Test]
    public async Task Put_WhenNamed_ShouldLetPendingListedViewerLoadPlayView()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var pending = TestUser.NewPending();
        using var viewer = ClientFor(pending);

        await SharingApi.PutOkAsync(Client, deck, "named", pending.Email);
        var play = await viewer.GetAsync($"/api/play/{deck}");
        var sharing = await viewer.GetAsync($"/api/decks/{deck}/sharing");

        play.StatusCode.ShouldBe(HttpStatusCode.OK);
        sharing.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await sharing.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("pending_approval");
    }

    [Test]
    public async Task Put_WhenNamed_ShouldRefuseUnlistedSignedInUser()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        using var mira = ClientFor(TestUser.Mira);
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var stranger = ClientFor(TestUser.NewPending());

        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email);

        foreach (var client in new[] { mira, anonymous, stranger })
        {
            var response = await client.GetAsync($"/api/play/{deck}");
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
        }
    }

    // The list is kept when the level changes, but it only lets people in while the level is named.
    [Test]
    public async Task Put_WhenBackToPrivate_ShouldRefuseFormerViewer()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        using var alex = ClientFor(TestUser.Alex);
        using var anonymous = ClientFor(TestUser.Anonymous);
        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email);
        (await alex.GetAsync($"/api/play/{deck}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await SharingApi.PutOkAsync(Client, deck, "link", TestUser.Alex.Email);
        (await anonymous.GetAsync($"/api/play/{deck}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var view = await SharingApi.PutOkAsync(Client, deck, "private", TestUser.Alex.Email);
        var alexResponse = await alex.GetAsync($"/api/play/{deck}");
        var anonymousResponse = await anonymous.GetAsync($"/api/play/{deck}");

        view.GetProperty("level").GetString().ShouldBe("private");
        alexResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await alexResponse.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/play/{deck}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("not-an-email")]
    [TestCase("Alex Chen <alex@team.example>")]
    [TestCase("alex@")]
    [TestCase("")]
    public async Task Put_WhenInvalidEmail_ShouldReturn400NamingIt(string bad)
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await SharingApi.PutAsync(Client, deck, new { level = "named", viewers = new[] { "alex@team.example", $" {bad} " } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe($"viewers: \"{bad}\" is not an email address");
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("level").GetString().ShouldBe("private");
    }

    // A level that isn't one of the names is K4's { "error" }, not a ProblemDetails. (The app-wide enum converter reads names
    // ignoring case, so "Link" binds here; set_sharing matches case-sensitively.)
    [TestCase("\"public\"")]
    [TestCase("\"\"")]
    [TestCase("1")]
    [TestCase("null")]
    public async Task Put_WhenInvalidLevel_ShouldReturn400Error(string level)
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await PutRawAsync(deck, $$"""{ "level": {{level}}, "viewers": [] }""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("\"title\"", Case.Sensitive);
        JsonDocument.Parse(body).RootElement.GetProperty("error").GetString().ShouldBe("level: must be \"private\", \"link\" or \"named\"");
    }

    [TestCase("""{ "viewers": [] }""", "level: must be \"private\", \"link\" or \"named\"")]
    [TestCase("""{ "level": "named" }""", "viewers: must be a list of email addresses")]
    [TestCase("""{ "level": "named", "viewers": [1] }""", "viewers: must be a list of email addresses")]
    public async Task Put_WhenFieldMissingOrWrongType_ShouldReturn400Error(string json, string error)
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await PutRawAsync(deck, json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe(error);
    }

    [Test]
    public async Task Put_WhenNotOwner_ShouldReturn404()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email);
        using var alex = ClientFor(TestUser.Alex);

        var put = await SharingApi.PutAsync(alex, deck, new { level = "link", viewers = Array.Empty<string>() });
        var get = await alex.GetAsync($"/api/decks/{deck}/sharing");
        var unknown = await SharingApi.PutAsync(Client, "nOsUcHdEcK00", new { level = "link", viewers = Array.Empty<string>() });

        foreach (var response in new[] { put, get, unknown })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
        }
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("level").GetString().ShouldBe("named");
    }

    // A change stamps Deck.UpdatedAt, so the library moves the deck up, and records exactly one activity entry. A PUT that
    // changes nothing records nothing.
    [Test]
    public async Task Put_WhenChanged_ShouldStampUpdatedAndRecordOneActivityEntry()
    {
        var shared = await SharingApi.CreateDeckAsync(Client, "Shared later");
        Clock.Advance(TimeSpan.FromMinutes(10));
        var other = await SharingApi.CreateDeckAsync(Client, "Made later");
        Clock.Advance(TimeSpan.FromMinutes(5));

        await SharingApi.PutOkAsync(Client, shared, "link");
        await SharingApi.PutOkAsync(Client, shared, "link");
        await SharingApi.PutOkAsync(Client, shared, "named", "mira@team.example");
        var library = await Client.GetFromJsonAsync<JsonElement>("/api/decks");

        var rows = library.GetProperty("decks").EnumerateArray().Where(r => r.GetProperty("id").GetString() is var id && (id == shared || id == other)).ToList();
        rows.Select(r => r.GetProperty("id").GetString()).ShouldBe([shared, other]);
        rows[0].GetProperty("updatedLabel").GetString().ShouldBe("just now");
        rows[0].GetProperty("sharing").GetString().ShouldBe("named");
        rows[0].GetProperty("viewerCount").GetInt32().ShouldBe(1);
        rows[0].GetProperty("lastChange").GetProperty("summary").GetString().ShouldBe("changed sharing: Anyone with the link → Named people (+mira@team.example)");
        rows[1].GetProperty("updatedLabel").GetString().ShouldBe("5 min ago");
        var activity = await DeckSeed.ActivityAsync(Client, shared);
        activity.Select(a => a.GetProperty("summary").GetString()).ShouldBe(
        [
            "changed sharing: Anyone with the link → Named people (+mira@team.example)",
            "changed sharing: Private → Anyone with the link",
            "created the deck",
        ]);
        activity[1].GetProperty("tool").ValueKind.ShouldBe(JsonValueKind.Null);
        activity[1].GetProperty("via").GetString().ShouldBe("web");
    }

    // sharedWithMe on GET /api/decks (DeckQueries): decks shared with the caller by name, whatever the case of the email
    // the owner typed, with the owner's name. Not a link deck, and not once the owner stops naming them.
    [Test]
    public async Task Put_WhenNamed_ShouldListDeckInViewersSharedWithMe()
    {
        var named = await SharingApi.CreateDeckAsync(Client, "Named for Alex");
        var linked = await SharingApi.CreateDeckAsync(Client, "Linked");
        using var alex = ClientFor(TestUser.Alex);
        await SharingApi.PutOkAsync(Client, named, "named", "Alex@Team.Example");
        await SharingApi.PutOkAsync(Client, linked, "link", TestUser.Alex.Email);

        var response = await alex.GetAsync("/api/decks");
        var body = await response.Content.ReadAsStringAsync();
        await SharingApi.PutOkAsync(Client, named, "private", TestUser.Alex.Email);
        var after = await alex.GetFromJsonAsync<JsonElement>("/api/decks");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var library = JsonDocument.Parse(body).RootElement;
        JsonShape.AssertMatches(library, "decks-library.json", ScreenShapes.DecksLibrary);
        var shared = library.GetProperty("sharedWithMe").EnumerateArray().Where(r => r.GetProperty("id").GetString() is var id && (id == named || id == linked)).ToList();
        shared.Count.ShouldBe(1);
        shared[0].GetProperty("id").GetString().ShouldBe(named);
        shared[0].GetProperty("title").GetString().ShouldBe("Named for Alex");
        shared[0].GetProperty("ownerName").GetString().ShouldBe(TestUser.Sam.Name);
        shared[0].GetProperty("sharing").GetString().ShouldBe("named");
        shared[0].GetProperty("playUrl").GetString().ShouldBe($"/d/{named}/named-for-alex");
        body.ShouldContain("\"sharing\":\"named\"", Case.Sensitive);
        after.GetProperty("sharedWithMe").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldNotContain(named);
    }

    private async Task<HttpResponseMessage> PutRawAsync(string deck, string json)
        => await Client.PutAsync($"/api/decks/{deck}/sharing", new StringContent(json, Encoding.UTF8, "application/json"));
}
