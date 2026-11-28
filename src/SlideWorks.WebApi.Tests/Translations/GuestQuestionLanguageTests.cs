using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 B3: the text of a poll, word-cloud or open-text slide reaches the phone in the guest's ?language=, on the poll GET,
// the answer's 409 and the response GET, while answers keep counting by option. Seeded with TranslationRowSeed and PollSeed.
[TestFixture]
[CancelAfter(60000)]
public class GuestQuestionLanguageTests : PollTestFixture
{
    private const string THAI_QUESTION = "คำขอผสานของคุณใหญ่แค่ไหน";
    private static readonly string[] _thaiOptions = ["น้อยกว่า 50 บรรทัด", "50 ถึง 200 บรรทัด", "มากกว่า 200 บรรทัด"];

    private TranslationRowSeed Translations => new(Factory.Services);

    [SetUp]
    public void NewMinute() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task Poll_WhenGuestReadsThai_ShouldShowThaiQuestionAndOptions()
    {
        var poll = await ThaiPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var guest = await JoinAsync(poll.Code);

        var thai = await ReadAsync(guest.GetPollAsync(poll.Code, "th"));
        var primary = await ReadAsync(guest.GetPollAsync(poll.Code));

        Activity(thai).GetProperty("question").GetString().ShouldBe(THAI_QUESTION);
        Options(thai).ShouldBe(_thaiOptions);
        Activity(primary).GetProperty("question").GetString().ShouldBe(PollSeed.QUESTION);
        Options(primary).ShouldBe(PollSeed.OPTIONS);
    }

    [TestCase("fr")]
    [TestCase("not-a-language")]
    public async Task Poll_WhenLanguageIsNotOffered_ShouldShowPrimary(string language)
    {
        var poll = await ThaiPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var guest = await JoinAsync(poll.Code);

        var view = await ReadAsync(guest.GetPollAsync(poll.Code, language));

        Activity(view).GetProperty("question").GetString().ShouldBe(PollSeed.QUESTION);
    }

    [Test]
    public async Task Poll_WhenTranslationsAreOff_ShouldShowPrimary()
    {
        var poll = await ThaiPollAsync();
        await Translations.SettingsAsync(poll.DeckId, false, "en", "th");
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var guest = await JoinAsync(poll.Code);

        var view = await ReadAsync(guest.GetPollAsync(poll.Code, "th"));

        Activity(view).GetProperty("question").GetString().ShouldBe(PollSeed.QUESTION);
    }

    [Test]
    public async Task Poll_WhenAnsweredInTwoLanguages_ShouldCountOnSameOption()
    {
        var poll = await ThaiPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var thai = await JoinAsync(poll.Code);
        await using var english = await JoinAsync(poll.Code);

        Options(await ReadAsync(thai.GetPollAsync(poll.Code, "th"))).ShouldBe(_thaiOptions);
        (await thai.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 0), "th")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await english.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 0))).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Rows.CloseAsync(round.Id, joinedAtClose: 2);
        var view = await ReadAsync(thai.GetPollAsync(poll.Code, "th"));

        view.GetProperty("tally").GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([0, 2, 0]);
    }

    [Test]
    public async Task PollAnswer_WhenConflict_ShouldReturnActivityInLanguage()
    {
        var poll = await ThaiPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var guest = await JoinAsync(poll.Code);

        var response = await guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 5), "th");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await ReadAsync(Task.FromResult(response), HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe("stale");
        Activity(body.GetProperty("current")).GetProperty("question").GetString().ShouldBe(THAI_QUESTION);
        Options(body.GetProperty("current")).ShouldBe(_thaiOptions);
    }

    [Test]
    public async Task ClosedRound_WhenSnapshotMatchesFrom_ShouldTranslate()
    {
        var poll = await ThaiPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);
        await using var guest = await JoinAsync(poll.Code);

        var view = await ReadAsync(guest.GetPollAsync(poll.Code, "th"));

        Activity(view).GetProperty("phase").GetString().ShouldBe("closed");
        Options(view).ShouldBe(_thaiOptions);
    }

    [Test]
    public async Task ClosedRound_WhenSnapshotDiffers_ShouldShowPrimary()
    {
        var poll = await ThaiPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);
        // The English of option 0 was edited after the round closed, so the entry no longer matches the snapshot's text.
        await Translations.LinesAsync(poll.DeckId, poll.Slide, Lines(("/options/0", _thaiOptions[0], "Under 40 lines"), ("/options/1", _thaiOptions[1], PollSeed.OPTIONS[1])));
        await using var guest = await JoinAsync(poll.Code);

        var view = await ReadAsync(guest.GetPollAsync(poll.Code, "th"));

        Options(view).ShouldBe([PollSeed.OPTIONS[0], _thaiOptions[1], PollSeed.OPTIONS[2]]);
    }

    [Test]
    public async Task WordCloud_WhenGuestReadsJapanese_ShouldShowJapaneseQuestion()
    {
        var room = await OpenResponseRoomAsync("word-cloud", "One word for how this sprint felt?");
        await Translations.SettingsAsync(room.DeckId, true, "en", "ja");
        await Translations.LinesAsync(room.DeckId, room.Slide, Lines("ja", ("/question", "このスプリントを一言で言うと?", "One word for how this sprint felt?")));
        await using var guest = await JoinAsync(room.Code);

        var japanese = await ReadAsync(guest.GetResponseAsync(room.Code, room.Slide, "ja"));
        var primary = await ReadAsync(guest.GetResponseAsync(room.Code, room.Slide, "th"));

        japanese.GetProperty("question").GetString().ShouldBe("このスプリントを一言で言うと?");
        primary.GetProperty("question").GetString().ShouldBe("One word for how this sprint felt?");
    }

    [Test]
    public async Task OpenText_WhenLanguageUnknown_ShouldShowPrimary()
    {
        var room = await OpenResponseRoomAsync("open-text", "What would help?");
        await Translations.SettingsAsync(room.DeckId, true, "en", "th");
        await Translations.LinesAsync(room.DeckId, room.Slide, Lines("th", ("/question", "อะไรจะช่วยได้", "What would help?")));
        await using var guest = await JoinAsync(room.Code);

        var unknown = await ReadAsync(guest.GetResponseAsync(room.Code, room.Slide, "xx"));
        var thai = await ReadAsync(guest.GetResponseAsync(room.Code, room.Slide, "th"));

        unknown.GetProperty("question").GetString().ShouldBe("What would help?");
        thai.GetProperty("question").GetString().ShouldBe("อะไรจะช่วยได้");
    }

    // A poll deck with translations on for Thai and the question and every option translated from the current English.
    private async Task<SeededPoll> ThaiPollAsync()
    {
        var poll = await SeedPollAsync();
        await Translations.SettingsAsync(poll.DeckId, true, "en", "th");
        await Translations.LinesAsync(poll.DeckId, poll.Slide, Lines(
            ("/question", THAI_QUESTION, PollSeed.QUESTION),
            ("/options/0", _thaiOptions[0], PollSeed.OPTIONS[0]),
            ("/options/1", _thaiOptions[1], PollSeed.OPTIONS[1]),
            ("/options/2", _thaiOptions[2], PollSeed.OPTIONS[2])));
        return poll;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, (string Text, string From)>> Lines(params (string Path, string Text, string From)[] lines)
        => Lines("th", lines);

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, (string Text, string From)>> Lines(string language, params (string Path, string Text, string From)[] lines)
        => new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            [language] = lines.ToDictionary(line => line.Path, line => (line.Text, line.From)),
        };

    private async Task<GuestClient> JoinAsync(string code)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        return guest;
    }

    private async Task<OpenRoom> OpenResponseRoomAsync(string layout, string question)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, layout, JsonSerializer.Serialize(new { question }));
        using var live = await Client.PostAsync($"/api/decks/{deck}/live", null);
        using var liveBody = JsonDocument.Parse(await live.Content.ReadAsStringAsync());
        using var opened = await Client.PostAsync($"/api/decks/{deck}/slides/1/response/open", null);
        opened.EnsureSuccessStatusCode();
        return new OpenRoom(deck, liveBody.RootElement.GetProperty("code").GetString()!, 1);
    }

    private static async Task<JsonElement> ReadAsync(Task<HttpResponseMessage> request, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await request;
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static JsonElement Activity(JsonElement view) => view.GetProperty("activity");

    private static IEnumerable<string?> Options(JsonElement view) => Activity(view).GetProperty("options").EnumerateArray().Select(o => o.GetString());

    private sealed record OpenRoom(string DeckId, string Code, int Slide);
}
