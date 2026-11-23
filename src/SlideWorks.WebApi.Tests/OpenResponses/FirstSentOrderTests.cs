using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

// An entry is ordered by when its guest first sent (OpenEntry.Arrival), not by guest id, random entry id or SQLite rowid.
// The guest ids below are chosen so that the guest who sends first has the highest id.
[TestFixture]
[CancelAfter(60000)]
public class FirstSentOrderTests : ApiTestFixture
{
    private static readonly string EARLY_SENDER = new('f', 32);
    private static readonly string MIDDLE_SENDER = new('8', 32);
    private static readonly string LATE_SENDER = new('0', 32);

    private readonly List<HttpClient> _clients = [];
    private readonly List<GuestClient> _guestClients = [];

    [TearDown]
    public async Task DisposeClients()
    {
        foreach (var guest in _guestClients)
        {
            await guest.DisposeAsync();
        }
        _guestClients.Clear();
        foreach (var client in _clients)
        {
            client.Dispose();
        }
        _clients.Clear();
    }

    [Test]
    public async Task Answers_WhenGuestsSendOutOfGuestIdOrder_ShouldNumberThemByArrival()
    {
        var room = await OpenTextRoomAsync();
        await JoinAsync(room, EARLY_SENDER, MIDDLE_SENDER, LATE_SENDER);
        await PutAnswerAsync(room, EARLY_SENDER, 0, "Early answer");
        Clock.Advance(TimeSpan.FromSeconds(5));
        await PutAnswerAsync(room, LATE_SENDER, 1, "Late answer");

        var view = await ControlTextsAsync(room);

        view.ShouldBe(["1:Early answer", "2:Late answer"]);
    }

    [Test]
    public async Task Answers_WhenALaterAnswerArrivesOrTheFirstGuestEdits_ShouldKeepEveryNumber()
    {
        var room = await OpenTextRoomAsync();
        await JoinAsync(room, EARLY_SENDER, MIDDLE_SENDER, LATE_SENDER);
        await PutAnswerAsync(room, EARLY_SENDER, 0, "Early answer");
        Clock.Advance(TimeSpan.FromSeconds(5));
        await PutAnswerAsync(room, LATE_SENDER, 1, "Late answer");
        (await ControlTextsAsync(room)).ShouldBe(["1:Early answer", "2:Late answer"]);

        Clock.Advance(TimeSpan.FromSeconds(5));
        await PutAnswerAsync(room, MIDDLE_SENDER, 2, "Middle answer");
        var afterThird = await ControlTextsAsync(room);
        Clock.Advance(TimeSpan.FromSeconds(5));
        await PutAnswerAsync(room, EARLY_SENDER, 3, "Early answer revised");
        var afterEdit = await ControlTextsAsync(room);

        afterThird.ShouldBe(["1:Early answer", "2:Late answer", "3:Middle answer"]);
        afterEdit.ShouldBe(["1:Early answer revised", "2:Late answer", "3:Middle answer"]);
    }

    [Test]
    public async Task Answers_WhenSentAtTheSameInstant_ShouldKeepArrivalOrder()
    {
        var room = await OpenTextRoomAsync();
        await JoinAsync(room, EARLY_SENDER, MIDDLE_SENDER, LATE_SENDER);
        await PutAnswerAsync(room, MIDDLE_SENDER, 0, "First in");
        await PutAnswerAsync(room, LATE_SENDER, 1, "Second in");
        await PutAnswerAsync(room, EARLY_SENDER, 2, "Third in");

        var view = await ControlTextsAsync(room);

        view.ShouldBe(["1:First in", "2:Second in", "3:Third in"]);
    }

    [Test]
    public async Task Wall_WhenGuestsSendOutOfGuestIdOrder_ShouldNumberAsTheControlsDo()
    {
        var room = await OpenTextRoomAsync();
        await JoinAsync(room, EARLY_SENDER, LATE_SENDER);
        await using var owner = await HubReceiver.StartAsync(Factory, TestUser.Sam);
        await owner.Connection.InvokeAsync("WatchAudience", room.DeckId);
        await PutAnswerAsync(room, EARLY_SENDER, 0, "Early answer");
        Clock.Advance(TimeSpan.FromSeconds(5));
        await PutAnswerAsync(room, LATE_SENDER, 1, "Late answer");

        var wall = await NextOpenTextAsync(owner.NextAudienceAsync, room.BaseRevision + 2);

        var shown = wall.GetProperty("answers").EnumerateArray().Select(a => $"{a.GetProperty("number").GetInt32()}:{a.GetProperty("text").GetString()}").ToList();
        shown.ShouldBe(await ControlTextsAsync(room));
        shown.ShouldBe(["1:Early answer", "2:Late answer"]);
    }

    [Test]
    public async Task Words_WhenAGuestReplacesTheirWords_ShouldKeepTheirPlaceInArrival()
    {
        var room = await OpenWordRoomAsync();
        await JoinAsync(room, EARLY_SENDER, LATE_SENDER);
        await PutWordsAsync(room, EARLY_SENDER, 0, "Calm", "Busy");
        Clock.Advance(TimeSpan.FromSeconds(5));
        await PutWordsAsync(room, LATE_SENDER, 1, "Tired");

        await PutWordsAsync(room, EARLY_SENDER, 2, "Quiet");

        (await ArrivalsAsync(room.RoundId)).ShouldBe(["Tired:2", "Quiet:1"], ignoreOrder: true);
    }

    // Both guests send the same word. The guest with the highest id sends first, in lower case: the ordinal-least casing
    // ("Calm") and the lowest guest id's casing are both the later sender's, so only first-sent order shows "calm".
    [Test]
    public async Task Cloud_WhenGuestsSendTheSameWordInDifferentCasing_ShouldShowTheFirstSendersCasing()
    {
        var room = await OpenWordRoomAsync();
        await JoinAsync(room, EARLY_SENDER, LATE_SENDER);
        await using var owner = await HubReceiver.StartAsync(Factory, TestUser.Sam);
        await owner.Connection.InvokeAsync("WatchAudience", room.DeckId);
        await PutWordsAsync(room, EARLY_SENDER, 0, "calm");
        Clock.Advance(TimeSpan.FromSeconds(5));
        await PutWordsAsync(room, LATE_SENDER, 1, "Calm");

        var wall = await NextWordCloudAsync(owner.NextAudienceAsync, room.BaseRevision + 2);

        (await TextsAsync(room.RoundId)).ShouldBe(["calm", "calm"]);
        wall.GetProperty("words").EnumerateArray().Select(w => w.GetProperty("text").GetString()).ShouldBe(["calm"]);
        using var response = await Client.GetAsync($"/api/decks/{room.DeckId}/slides/{room.Slide}/response");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.GetProperty("entries").GetArrayLength().ShouldBe(1);
        body.GetProperty("entries")[0].GetProperty("text").GetString().ShouldBe("calm");
        body.GetProperty("entries")[0].GetProperty("count").GetInt32().ShouldBe(2);
    }

    // Reader ordering only: submitting through the routes stores every row of a key in one casing, so rows that differ in casing
    // exist only from before SW-672. These two tests write such rows straight to the database, the one setup public routes cannot reach.
    [Test]
    public async Task Cloud_WhenStoredRowsShareAKeyWithDifferentCasing_ShouldShowTheEarliestRowsCasing()
    {
        var room = await OpenWordRoomAsync();
        await SeedEntriesAsync(room.RoundId, ("a-late", 2, "Calm"), ("z-first", 1, "calm"));

        using var response = await Client.GetAsync($"/api/decks/{room.DeckId}/slides/{room.Slide}/response");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.GetProperty("entries").GetArrayLength().ShouldBe(1);
        body.GetProperty("entries")[0].GetProperty("text").GetString().ShouldBe("calm");
        body.GetProperty("entries")[0].GetProperty("count").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task Cloud_WhenStoredRowsShareAKeyWithDifferentCasing_ShouldShowTheSameCasingOnTheWall()
    {
        var room = await OpenWordRoomAsync();
        await SeedEntriesAsync(room.RoundId, ("a-late", 2, "Calm"), ("z-first", 1, "calm"));
        await using var owner = await HubReceiver.StartAsync(Factory, TestUser.Sam);
        await owner.Connection.InvokeAsync("WatchAudience", room.DeckId);
        await JoinAsync(room, LATE_SENDER);

        await PutWordsAsync(room, LATE_SENDER, 0, "Other");
        while (true)
        {
            var frame = await owner.NextAudienceAsync();
            if (frame.GetProperty("type").GetString() != "wordCloud" || frame.GetProperty("revision").GetInt32() != room.BaseRevision + 1)
            {
                continue;
            }
            var words = frame.GetProperty("words").EnumerateArray().Select(w => w.GetProperty("text").GetString()).ToList();
            words.ShouldContain("calm");
            words.ShouldNotContain("Calm");
            return;
        }
    }

    [Test]
    public async Task Migration_WhenEntriesExistBeforeIt_ShouldKeepTheirWallOrderPerRound()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sw-migration-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options);
            var migrator = db.GetService<IMigrator>();
            var all = db.Database.GetMigrations().ToList();
            var sw672 = all.Single(m => m.EndsWith("_Sw672OpenEntryFirstSent", StringComparison.Ordinal));
            await migrator.MigrateAsync(all[all.IndexOf(sw672) - 1]);
            var (roundA, roundB) = await SeedOldEntriesAsync(db);

            await migrator.MigrateAsync();

            var rows = await db.Database.SqlQueryRaw<string>("""SELECT "RoundId" || '|' || "Text" || '|' || "Arrival" AS Value FROM "OpenEntries" ORDER BY rowid""").ToListAsync();
            rows.ShouldBe([$"{roundA}|zebra|1", $"{roundB}|first|1", $"{roundA}|apple|2", $"{roundB}|second|2", $"{roundA}|mango|3"]);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    // Two rounds whose rows were inserted interleaved, so the per-round order is the rowid order, not the table's.
    private static async Task<(string RoundA, string RoundB)> SeedOldEntriesAsync(AppDbContext db)
    {
        var owner = Upper(Guid.NewGuid());
        var (deck, slideA, slideB, session) = ("deck0001", Upper(Guid.NewGuid()), Upper(Guid.NewGuid()), Upper(Guid.NewGuid()));
        var (responseA, responseB, roundA, roundB) = (Upper(Guid.NewGuid()), Upper(Guid.NewGuid()), Upper(Guid.NewGuid()), Upper(Guid.NewGuid()));
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, Email, Name, GoogleSubject, CreatedAt, Access, IsAdmin) VALUES ({0}, 'sam@example.com', 'Sam', 'sub-1', 0, 'Approved', 0)", owner);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Decks (Id, Slug, Title, PartsJson, OwnerId, Sharing, Revision, CreatedAt, UpdatedAt, Template, Palette, Draft, JoinCode) "
            + "VALUES ({0}, 'd', 'Deck', '[]', {1}, 'Private', 0, 0, 0, 'talk', 'cobalt', 0, '123456')", deck, owner);
        foreach (var (slide, position) in new[] { (slideA, 0), (slideB, 1) })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Slides (Id, DeckId, Position, Layout, SlotsJson, Steps, Revision, DeepDive) VALUES ({0}, {1}, {2}, 'word-cloud', '{{}}', 0, 1, 0)", slide, deck, position);
        }
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO AudienceSessions (Id, DeckId, StartedAt, PeakJoined, EndedAt) VALUES ({0}, {1}, 0, 0, NULL)", session, deck);
        foreach (var (response, slide) in new[] { (responseA, slideA), (responseB, slideB) })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO OpenResponses (Id, DeckId, SlideId, Kind) VALUES ({0}, {1}, {2}, 'WordCloud')", response, deck, slide);
        }
        foreach (var (round, response) in new[] { (roundA, responseA), (roundB, responseB) })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO OpenRounds (Id, OpenResponseId, SessionId, Number, Revision) VALUES ({0}, {1}, {2}, 1, 0)", round, response, session);
        }
        var rows = new[] { (roundA, "g1", "zebra"), (roundB, "g1", "first"), (roundA, "g2", "apple"), (roundB, "g2", "second"), (roundA, "g3", "mango") };
        foreach (var (round, guest, text) in rows)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO OpenEntries (Id, RoundId, GuestId, Position, Text, TextKey) VALUES ({0}, {1}, {2}, 0, {3}, {3})", Upper(Guid.NewGuid()), round, guest, text);
        }
        return (roundA, roundB);
    }

    private static string Upper(Guid id) => id.ToString().ToUpperInvariant();

    private async Task<Room> OpenTextRoomAsync() => await OpenRoomAsync("open-text", """{"question":"What would help?"}""");

    private async Task<Room> OpenWordRoomAsync() => await OpenRoomAsync("word-cloud", """{"question":"How do you feel?"}""");

    // A deck with one slide, taken live, its round opened through the open route.
    private async Task<Room> OpenRoomAsync(string layout, string slots)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deckId = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deckId, layout, slots);
        using var live = await Client.PostAsync($"/api/decks/{deckId}/live", null);
        var body = await live.Content.ReadAsStringAsync();
        live.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var sessionId = Guid.Parse(document.RootElement.GetProperty("sessionId").GetString()!);
        var code = document.RootElement.GetProperty("code").GetString()!;
        using var opened = await Client.PostAsync($"/api/decks/{deckId}/slides/1/response/open", null);
        var view = await opened.Content.ReadFromJsonAsync<JsonElement>();
        opened.StatusCode.ShouldBe(HttpStatusCode.OK, view.ToString());
        return new Room(deckId, code, sessionId, Guid.Parse(view.GetProperty("round").GetString()!), 1, view.GetProperty("revision").GetInt32());
    }

    // Each guest joins the live session over the hub with the given sw_guest id, as the join page does.
    private async Task JoinAsync(Room room, params string[] guests)
    {
        foreach (var guest in guests)
        {
            var client = await GuestClient.ConnectAsync(Factory, guestId: guest);
            _guestClients.Add(client);
            await client.Connection.InvokeAsync<JsonElement>("JoinAudience", room.Code);
        }
    }

    private async Task SeedEntriesAsync(Guid roundId, params (string Guest, long Arrival, string Text)[] rows)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.OpenEntries.AddRange(rows.Select(row => new OpenEntry
        {
            Id = Guid.NewGuid(),
            RoundId = roundId,
            GuestId = row.Guest,
            Position = 0,
            Text = row.Text,
            TextKey = row.Text.ToLowerInvariant(),
            Arrival = row.Arrival,
        }));
        await db.SaveChangesAsync();
    }

    private async Task<List<string>> TextsAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OpenEntries.AsNoTracking().Where(e => e.RoundId == roundId).OrderBy(e => e.Arrival).Select(e => e.Text).ToListAsync();
    }

    private async Task PutAnswerAsync(Room room, string guestId, int revision, string text)
    {
        using var response = await SendAsync(room, guestId, "answer", new { round = room.RoundId, revision = room.BaseRevision + revision, text });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task PutWordsAsync(Room room, string guestId, int revision, params string[] words)
    {
        using var response = await SendAsync(room, guestId, "words", new { round = room.RoundId, revision = room.BaseRevision + revision, words });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> SendAsync(Room room, string guestId, string route, object body)
    {
        var http = ClientFor(TestUser.Anonymous);
        _clients.Add(http);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/join/{room.Code}/slides/{room.Slide}/{route}") { Content = JsonContent.Create(body) };
        request.Headers.Add("Cookie", $"{GuestCookie.NAME}={guestId}");
        return await http.SendAsync(request);
    }

    private async Task<List<string>> ControlTextsAsync(Room room)
    {
        using var response = await Client.GetAsync($"/api/decks/{room.DeckId}/slides/{room.Slide}/response");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        return [.. body.GetProperty("entries").EnumerateArray().Select(e => $"{e.GetProperty("number").GetInt32()}:{e.GetProperty("text").GetString()}")];
    }

    private async Task<List<string>> ArrivalsAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.OpenEntries.AsNoTracking().Where(e => e.RoundId == roundId).ToListAsync();
        return [.. rows.Select(row => $"{row.Text}:{row.Arrival}")];
    }

    private static async Task<JsonElement> NextWordCloudAsync(Func<Task<JsonElement>> next, int revision)
    {
        while (true)
        {
            var frame = await next();
            if (frame.GetProperty("type").GetString() == "wordCloud" && frame.GetProperty("revision").GetInt32() == revision)
            {
                return frame;
            }
        }
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

    private sealed record Room(string DeckId, string Code, Guid SessionId, Guid RoundId, int Slide, int BaseRevision);
}
