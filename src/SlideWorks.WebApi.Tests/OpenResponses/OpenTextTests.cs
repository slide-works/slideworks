using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OpenResponses;

// A guest sends one answer on an open-text slide and the wall shows it in number order. The round is seeded.
[TestFixture]
[CancelAfter(30000)]
public class OpenTextTests : ApiTestFixture
{
    private const string QUESTION = "What would help your team ship smaller changes?";

    [Test]
    public async Task Get_WhenThereIsNoGuestCookie_ShouldSucceedAndSetOne()
    {
        var room = await OpenRoomAsync();
        var http = ClientFor(TestUser.Anonymous);
        _clients.Add(http);

        using var response = await http.GetAsync($"/api/join/{room.Code}/slides/{room.Slide}/response");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("kind").GetString().ShouldBe("open-text");
        json.RootElement.GetProperty("mineText").GetString().ShouldBe("");
        response.Headers.GetValues("Set-Cookie").Single().ShouldMatch("^sw_guest=[0-9a-f]{32}; ");
    }

    [Test]
    public async Task Answer_WhenTextIsSent_ShouldStoreIt()
    {
        var room = await OpenRoomAsync();
        var guest = GuestCookie.NewId();
        await JoinRosterAsync(room.SessionId, guest);

        using var response = await PutAsync(room, guest, 0, "A shared   definition of done ");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldBe("""{"text":"A shared definition of done"}""");
        var stored = await StoredAsync(room.RoundId);
        stored.Select(row => row.Position).ShouldBe([0]);
        stored.Select(row => row.Text).ShouldBe(["A shared definition of done"]);
        stored.Select(row => row.TextKey).ShouldBe(["a shared definition of done"]);
        (await RevisionAsync(room.RoundId)).ShouldBe(1);
    }

    [Test]
    public async Task Wall_WhenAnAnswerIsStored_ShouldShowItInOrder()
    {
        var room = await OpenRoomAsync();
        await using var first = await GuestClient.ConnectAsync(Factory);
        await using var second = await GuestClient.ConnectAsync(Factory);
        await first.Connection.InvokeAsync<JsonElement>("JoinAudience", room.Code);
        await second.Connection.InvokeAsync<JsonElement>("JoinAudience", room.Code);
        (await PutAsync(room, first.GuestId, 0, "Zebra")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var response = await PutAsync(room, second.GuestId, 1, "Apple"))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
        var shown = await NextOpenTextAsync(second.NextAudienceAsync, 2);

        shown.GetProperty("type").GetString().ShouldBe("openText");
        shown.GetProperty("revealed").GetBoolean().ShouldBeTrue();
        shown.GetProperty("guests").GetInt32().ShouldBe(2);
        shown.GetProperty("answers").GetArrayLength().ShouldBe(2);
        shown.GetProperty("answers")[0].GetProperty("number").GetInt32().ShouldBe(1);
        shown.GetProperty("answers")[0].GetProperty("text").GetString().ShouldBe("Zebra");
        shown.GetProperty("answers")[0].GetProperty("hidden").GetBoolean().ShouldBeFalse();
        shown.GetProperty("answers")[1].GetProperty("number").GetInt32().ShouldBe(2);
        shown.GetProperty("answers")[1].GetProperty("text").GetString().ShouldBe("Apple");
        shown.GetProperty("answers")[1].GetProperty("hidden").GetBoolean().ShouldBeFalse();

        await using var owner = await HubReceiver.StartAsync(Factory, TestUser.Sam);
        await owner.Connection.InvokeAsync("WatchAudience", room.DeckId);
        await HideAsync(room.RoundId, second.GuestId);
        using (var response = await PutAsync(room, first.GuestId, 2, "Zebra again"))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
        var audience = await NextOpenTextAsync(first.NextAudienceAsync, 3);
        var presenter = await NextOpenTextAsync(owner.NextAudienceAsync, 3);

        audience.GetProperty("answers").GetArrayLength().ShouldBe(1);
        audience.GetProperty("answers")[0].GetProperty("number").GetInt32().ShouldBe(1);
        audience.GetProperty("answers")[0].GetProperty("text").GetString().ShouldBe("Zebra again");
        audience.GetRawText().ShouldNotContain("Apple", Case.Sensitive);
        presenter.GetProperty("answers").GetArrayLength().ShouldBe(2);
        presenter.GetProperty("answers")[0].GetProperty("number").GetInt32().ShouldBe(1);
        presenter.GetProperty("answers")[0].GetProperty("text").GetString().ShouldBe("Zebra again");
        presenter.GetProperty("answers")[0].GetProperty("hidden").GetBoolean().ShouldBeFalse();
        presenter.GetProperty("answers")[1].GetProperty("number").GetInt32().ShouldBe(2);
        presenter.GetProperty("answers")[1].GetProperty("text").GetString().ShouldBe("Apple");
        presenter.GetProperty("answers")[1].GetProperty("hidden").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task Answer_WhenTextIsOver200_ShouldRefuseAndStoreNothing()
    {
        var room = await OpenRoomAsync();
        var guest = GuestCookie.NewId();
        await JoinRosterAsync(room.SessionId, guest);

        using var response = await PutAsync(room, guest, 0, new string('a', 201));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"text_too_long"}""");
        (await StoredAsync(room.RoundId)).ShouldBeEmpty();
        (await RevisionAsync(room.RoundId)).ShouldBe(0);
    }

    [Test]
    public async Task Answer_WhenTheTextIsProfane_ShouldKeepThePreviousRow()
    {
        var room = await OpenRoomAsync();
        var guest = GuestCookie.NewId();
        await JoinRosterAsync(room.SessionId, guest);
        (await PutAsync(room, guest, 0, "Keep this answer")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PutAsync(room, guest, 1, "Keep this shit");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"profane"}""");
        (await StoredAsync(room.RoundId)).Select(row => row.Text).ShouldBe(["Keep this answer"]);
        (await RevisionAsync(room.RoundId)).ShouldBe(1);
    }

    [Test]
    public async Task Answer_WhenTheGuestSendsAgain_ShouldKeepTheSameNumber()
    {
        var room = await OpenRoomAsync();
        var first = GuestCookie.NewId();
        var second = GuestCookie.NewId();
        await JoinRosterAsync(room.SessionId, first);
        await JoinRosterAsync(room.SessionId, second);
        (await PutAsync(room, first, 0, "First")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutAsync(room, second, 1, "Second")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PutAsync(room, first, 2, "First revised");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var stored = await StoredAsync(room.RoundId);
        stored.Select(row => row.GuestId).ShouldBe([first, second]);
        stored.Select(row => row.Text).ShouldBe(["First revised", "Second"]);
        stored.Select(row => row.Position).ShouldBe([0, 0]);
    }

    [Test]
    public async Task Answer_WhenTheRevisionIsStale_ShouldRefuse()
    {
        var room = await OpenRoomAsync();
        var guest = GuestCookie.NewId();
        await JoinRosterAsync(room.SessionId, guest);
        (await PutAsync(room, guest, 0, "Kept")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PutAsync(room, guest, 0, "Replaced");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"stale"}""");
        (await StoredAsync(room.RoundId)).Select(row => row.Text).ShouldBe(["Kept"]);
        (await RevisionAsync(room.RoundId)).ShouldBe(1);
    }

    private async Task<Room> OpenRoomAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deckId = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deckId, "open-text", JsonSerializer.Serialize(new { question = QUESTION }));
        using var live = await Client.PostAsync($"/api/decks/{deckId}/live", null);
        var body = await live.Content.ReadAsStringAsync();
        live.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var sessionId = Guid.Parse(document.RootElement.GetProperty("sessionId").GetString()!);
        var code = document.RootElement.GetProperty("code").GetString()!;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slide = await db.Slides.SingleAsync(s => s.DeckId == deckId && s.Layout == "open-text" && s.DeletedAt == null);
        var responseId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        db.OpenResponses.Add(new OpenResponse { Id = responseId, DeckId = deckId, SlideId = slide.Id, Kind = OpenResponseKind.OpenText });
        db.OpenRounds.Add(new OpenRound
        {
            Id = roundId,
            OpenResponseId = responseId,
            SessionId = sessionId,
            Number = 1,
            Revision = 0,
            OpenedAt = Clock.GetUtcNow(),
            RevealedAt = Clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
        return new Room(deckId, code, sessionId, roundId, slide.Position + 1);
    }

    private async Task JoinRosterAsync(Guid sessionId, string guestId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AudienceJoins.Add(new AudienceJoin { SessionId = sessionId, GuestId = guestId, JoinedAt = Clock.GetUtcNow() });
        await db.SaveChangesAsync();
    }

    private async Task HideAsync(Guid roundId, string guestId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.OpenEntries.SingleAsync(e => e.RoundId == roundId && e.GuestId == guestId);
        entry.HiddenAt = Clock.GetUtcNow();
        await db.SaveChangesAsync();
    }

    private readonly List<HttpClient> _clients = [];

    [TearDown]
    public void DisposeClients()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }
        _clients.Clear();
    }

    private async Task<HttpResponseMessage> PutAsync(Room room, string guestId, int revision, string text)
    {
        var http = ClientFor(TestUser.Anonymous);
        _clients.Add(http);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/join/{room.Code}/slides/{room.Slide}/answer")
        {
            Content = JsonContent.Create(new { round = room.RoundId, revision, text }),
        };
        request.Headers.Add("Cookie", $"{GuestCookie.NAME}={guestId}");
        return await http.SendAsync(request);
    }

    private async Task<IReadOnlyList<StoredRow>> StoredAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Database.SqlQuery<string>(
                $"SELECT \"Id\" AS Value FROM \"OpenEntries\" WHERE \"RoundId\" = {roundId.ToString().ToUpperInvariant()} AND \"Position\" = 0 ORDER BY rowid")
            .ToListAsync();
        var rows = await db.OpenEntries.AsNoTracking().Where(e => e.RoundId == roundId && e.Position == 0).ToListAsync();
        var rank = order.Select((id, index) => (id, index)).ToDictionary(row => Guid.Parse(row.id), row => row.index);
        return [.. rows.OrderBy(row => rank[row.Id]).Select(row => new StoredRow(row.GuestId, row.Position, row.Text, row.TextKey))];
    }

    private async Task<int> RevisionAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OpenRounds.Where(r => r.Id == roundId).Select(r => r.Revision).SingleAsync();
    }

    private static async Task<JsonElement> NextOpenTextAsync(Func<Task<JsonElement>> next, int revision)
    {
        while (true)
        {
            var frame = await next();
            if (frame.GetProperty("type").GetString() == "openText" && frame.GetProperty("revision").GetInt32() == revision)
            {
                return frame;
            }
        }
    }

    private sealed record Room(string DeckId, string Code, Guid SessionId, Guid RoundId, int Slide);

    private sealed record StoredRow(string GuestId, int Position, string Text, string TextKey);
}
