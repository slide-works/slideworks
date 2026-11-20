using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.OpenResponses;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Tests;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OpenResponses;

// A guest's words on a word-cloud slide: stored on the open round, replaced by the next submit, and refused with
// nothing written when the submit fails a check.
[TestFixture]
[CancelAfter(30000)]
public class WordSubmitTests : ApiTestFixture
{
    private const int SLIDE = 1;
    private const string QUESTION = "One word for this slice?";

    [Test]
    public async Task Submit_WhenThreeWordsAreSent_ShouldStoreThem()
    {
        var guest = GuestCookie.NewId();
        var other = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 4, guestId: guest, joined: true, (other, "Focused"));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await RowsAsync(seeded.RoundId);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 4, "focused", "  Busy ", "Calm"));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldBe("""{"words":["Focused","Busy","Calm"]}""");
        var mine = (await RowsAsync(seeded.RoundId)).Where(row => row.GuestId == guest).OrderBy(row => row.Position).ToList();
        mine.Select(row => row.Text).ShouldBe(["Focused", "Busy", "Calm"]);
        mine.Select(row => row.TextKey).ShouldBe(["focused", "busy", "calm"]);
        mine.Select(row => row.Position).ShouldBe([0, 1, 2]);
        (await RowsAsync(seeded.RoundId)).Single(row => row.GuestId == other).Id.ShouldBe(before.Single(row => row.GuestId == other).Id);
        (await RevisionAsync(seeded.RoundId)).ShouldBe(5);

        using var read = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{seeded.Code}/slides/{SLIDE}/response");
        read.Headers.Add("Cookie", $"{GuestCookie.NAME}={guest}");
        using var view = await anonymous.SendAsync(read);
        var viewBody = await view.Content.ReadAsStringAsync();
        view.StatusCode.ShouldBe(HttpStatusCode.OK, viewBody);
        using var json = JsonDocument.Parse(viewBody);
        json.RootElement.GetProperty("kind").GetString().ShouldBe("word-cloud");
        json.RootElement.GetProperty("phase").GetString().ShouldBe("open");
        json.RootElement.GetProperty("round").GetString().ShouldBe(seeded.RoundId.ToString());
        json.RootElement.GetProperty("revision").GetInt32().ShouldBe(5);
        json.RootElement.GetProperty("question").GetString().ShouldBe(QUESTION);
        json.RootElement.GetProperty("revealed").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("mineWords").EnumerateArray().Select(word => word.GetString()).ShouldBe(["Focused", "Busy", "Calm"]);
    }

    [Test]
    public async Task Submit_WhenAWordIsOneTypoFromAnotherGuestsWord_ShouldStoreItUnderThatWord()
    {
        var guest = GuestCookie.NewId();
        var other = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 1, guestId: guest, joined: true, (other, "Ship"), (other, "Focus"));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 1, "shipp", "Shipping", "Fcous"));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldBe("""{"words":["Ship","Shipping","Focus"]}""");
        var mine = (await RowsAsync(seeded.RoundId)).Where(row => row.GuestId == guest).OrderBy(row => row.Position).ToList();
        mine.Select(row => row.Text).ShouldBe(["Ship", "Shipping", "Focus"]);
        mine.Select(row => row.TextKey).ShouldBe(["ship", "shipping", "focus"]);
    }

    [Test]
    public async Task Submit_WhenShortWordsAreOneLetterApart_ShouldKeepThemApart()
    {
        var guest = GuestCookie.NewId();
        var other = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 1, guestId: guest, joined: true, (other, "Code"));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 1, "Core"));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldBe("""{"words":["Core"]}""");
        (await RowsAsync(seeded.RoundId)).Single(row => row.GuestId == guest).TextKey.ShouldBe("core");
    }

    [Test]
    public async Task Submit_WhenTwoNearWordsMatchTheSameWord_ShouldJoinTheOneMoreGuestsSent()
    {
        var guest = GuestCookie.NewId();
        var first = GuestCookie.NewId();
        var second = GuestCookie.NewId();
        var seeded = await SeedAsync(
            live: true, open: true, revision: 1, guestId: guest, joined: true, (first, "Teams"), (first, "Teamx"), (second, "Teamx"));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 1, "Teamz"));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldBe("""{"words":["Teamx"]}""");
    }

    [Test]
    public async Task Submit_WhenTheGuestSendsTwoNearWords_ShouldStoreOne()
    {
        var guest = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 1, guestId: guest, joined: true);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 1, "Teams", "team", "Calm"));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldBe("""{"words":["Teams","Calm"]}""");
        (await RowsAsync(seeded.RoundId)).Select(row => (row.Position, row.TextKey)).ShouldBe([(0, "teams"), (1, "calm")]);
    }

    [Test]
    public async Task Submit_WhenTheGuestReplacesTheirOwnWord_ShouldNotGroupItWithTheOldOne()
    {
        var guest = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 1, guestId: guest, joined: true, (guest, "Teams"));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 1, "team"));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldBe("""{"words":["team"]}""");
    }

    [Test]
    public async Task Submit_WhenAWordIsProfane_ShouldRefuseAndStoreNothing()
    {
        var guest = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 2, guestId: guest, joined: true, (guest, "Kept"));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await RowsAsync(seeded.RoundId);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 2, "Calm", "Bollocks!"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"profane"}""");
        (await RowsAsync(seeded.RoundId)).Select(row => row.Id).ShouldBe(before.Select(row => row.Id));
        (await RowsAsync(seeded.RoundId)).Single().Text.ShouldBe("Kept");
        (await RevisionAsync(seeded.RoundId)).ShouldBe(2);
    }

    [Test]
    public async Task Submit_WhenTheGuestSendsAgain_ShouldDropThePreviousWords()
    {
        var guest = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 2, guestId: guest, joined: true);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var first = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 2, "Alpha", "Beta"));
        var firstBody = await first.Content.ReadAsStringAsync();
        first.StatusCode.ShouldBe(HttpStatusCode.OK, firstBody);
        firstBody.ShouldBe("""{"words":["Alpha","Beta"]}""");

        using var second = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 3, "Gamma"));
        var secondBody = await second.Content.ReadAsStringAsync();
        second.StatusCode.ShouldBe(HttpStatusCode.OK, secondBody);
        secondBody.ShouldBe("""{"words":["Gamma"]}""");
        var rows = await RowsAsync(seeded.RoundId);
        rows.Select(row => row.Text).ShouldBe(["Gamma"]);
        rows.Single().GuestId.ShouldBe(guest);
        rows.Single().TextKey.ShouldBe("gamma");
    }

    [Test]
    public async Task Submit_WhenTheRoundIsClosed_ShouldRefuseAndStoreNothing()
    {
        var guest = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: false, revision: 6, guestId: guest, joined: true, (guest, "Kept"));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await RowsAsync(seeded.RoundId);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 6, "New"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"wrong_phase"}""");
        (await RowsAsync(seeded.RoundId)).Select(row => row.Id).ShouldBe(before.Select(row => row.Id));
        (await RowsAsync(seeded.RoundId)).Single().Text.ShouldBe("Kept");
        (await RevisionAsync(seeded.RoundId)).ShouldBe(6);
    }

    [Test]
    public async Task Submit_WhenTheRevisionIsStale_ShouldRefuseAndKeepTheRows()
    {
        var guest = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 5, guestId: guest, joined: true, (guest, "Kept"));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await RowsAsync(seeded.RoundId);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 4, "New"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"stale"}""");
        (await RowsAsync(seeded.RoundId)).Select(row => row.Id).ShouldBe(before.Select(row => row.Id));
        (await RowsAsync(seeded.RoundId)).Single().Text.ShouldBe("Kept");
        (await RevisionAsync(seeded.RoundId)).ShouldBe(5);
    }

    [Test]
    public async Task Submit_WhenTheGuestIsOverTheLimit_ShouldRefuse()
    {
        var guest = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 1, guestId: guest, joined: true);
        using var anonymous = ClientFor(TestUser.Anonymous);
        for (var sent = 0; sent < OpenResponseLimiter.LIMIT; sent++)
        {
            using var allowed = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 1 + sent, "One"));
            var allowedBody = await allowed.Content.ReadAsStringAsync();
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK, allowedBody);
        }

        var kept = (await RowsAsync(seeded.RoundId)).Single();
        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 1 + OpenResponseLimiter.LIMIT, "Two"));

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"too_many_answers"}""");
        var after = (await RowsAsync(seeded.RoundId)).Single();
        after.Id.ShouldBe(kept.Id);
        after.Text.ShouldBe("One");
        after.TextKey.ShouldBe("one");
    }

    [Test]
    public async Task Submit_WhenThereIsNoSession_ShouldRefuse()
    {
        var guest = GuestCookie.NewId();
        var seeded = await SeedAsync(live: false, open: true, revision: 1, guestId: guest, joined: false, (guest, "Kept"));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await RowsAsync(seeded.RoundId);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 1, "New"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"no_session"}""");
        (await RowsAsync(seeded.RoundId)).Select(row => row.Id).ShouldBe(before.Select(row => row.Id));
        (await RowsAsync(seeded.RoundId)).Single().Text.ShouldBe("Kept");
    }

    [Test]
    public async Task Submit_WhenTheGuestIsOffTheRoster_ShouldRefuse()
    {
        var guest = GuestCookie.NewId();
        var seeded = await SeedAsync(live: true, open: true, revision: 1, guestId: guest, joined: false, (guest, "Kept"));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = await RowsAsync(seeded.RoundId);

        using var response = await anonymous.SendAsync(Words(seeded.Code, guest, seeded.RoundId, 1, "New"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"no_guest"}""");
        (await RowsAsync(seeded.RoundId)).Select(row => row.Id).ShouldBe(before.Select(row => row.Id));
        (await RowsAsync(seeded.RoundId)).Single().Text.ShouldBe("Kept");
    }

    private async Task<Seeded> SeedAsync(
        bool live,
        bool open,
        int revision,
        string guestId,
        bool joined,
        params (string GuestId, string Text)[] entries)
    {
        using var created = await Client.PostAsJsonAsync("/api/decks", new { title = "Word cloud" });
        created.EnsureSuccessStatusCode();
        var deckId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Creating a deck returned no id.");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var code = await db.Decks.Where(d => d.Id == deckId).Select(d => d.JoinCode).SingleAsync();
        var now = Clock.GetUtcNow();
        var sessionId = Guid.NewGuid();
        db.AudienceSessions.Add(new AudienceSession
        {
            Id = sessionId,
            DeckId = deckId,
            StartedAt = now.AddHours(-4),
            EndedAt = live ? null : now.AddHours(-3),
        });
        if (joined)
        {
            db.AudienceJoins.Add(new AudienceJoin { SessionId = sessionId, GuestId = guestId, JoinedAt = now });
        }

        var slideId = Guid.NewGuid();
        db.Slides.Add(new Slide
        {
            Id = slideId,
            DeckId = deckId,
            Position = SLIDE - 1,
            Layout = "word-cloud",
            SlotsJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["question"] = QUESTION }),
            Steps = 0,
            Revision = 1,
        });
        var responseId = Guid.NewGuid();
        db.OpenResponses.Add(new OpenResponse
        {
            Id = responseId,
            DeckId = deckId,
            SlideId = slideId,
            Kind = OpenResponseKind.WordCloud,
        });
        var roundId = Guid.NewGuid();
        db.OpenRounds.Add(new OpenRound
        {
            Id = roundId,
            OpenResponseId = responseId,
            SessionId = sessionId,
            Number = 1,
            Revision = revision,
            OpenedAt = now.AddHours(-4),
            ClosedAt = open ? null : now,
            RevealedAt = now.AddHours(-4),
        });
        var next = new Dictionary<string, int>();
        foreach (var (who, text) in entries)
        {
            next.TryGetValue(who, out var position);
            next[who] = position + 1;
            db.OpenEntries.Add(new OpenEntry
            {
                Id = Guid.NewGuid(),
                RoundId = roundId,
                GuestId = who,
                Position = position,
                Text = text,
                TextKey = OpenTextRules.KeyOf(text),
            });
        }

        await db.SaveChangesAsync();
        return new Seeded(code, roundId);
    }

    private static HttpRequestMessage Words(string code, string guestId, Guid roundId, int revision, params string[] words)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/join/{code}/slides/{SLIDE}/words")
        {
            Content = JsonContent.Create(new { round = roundId.ToString(), revision, words }),
        };
        request.Headers.Add("Cookie", $"{GuestCookie.NAME}={guestId}");
        return request;
    }

    private async Task<IReadOnlyList<Held>> RowsAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OpenEntries.AsNoTracking()
            .Where(entry => entry.RoundId == roundId)
            .OrderBy(entry => entry.GuestId)
            .ThenBy(entry => entry.Position)
            .Select(entry => new Held(entry.Id, entry.GuestId, entry.Position, entry.Text, entry.TextKey))
            .ToListAsync();
    }

    private async Task<int> RevisionAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OpenRounds.AsNoTracking()
            .Where(round => round.Id == roundId)
            .Select(round => round.Revision)
            .SingleAsync();
    }

    private sealed record Seeded(string Code, Guid RoundId);

    private readonly record struct Held(Guid Id, string GuestId, int Position, string Text, string TextKey);
}
