using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K5: GET /api/join/{code} lists the languages a guest can pick, with how complete each is, and is null while off.
[TestFixture]
[CancelAfter(60000)]
public class GuestViewTranslationsTests : PollTestFixture
{
    [SetUp]
    public void NewMinute() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task GuestView_WhenTranslationsOn_ShouldListLanguagesWithComplete()
    {
        var poll = await SeedPollAsync();
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.SettingsAsync(poll.DeckId, true, "en", "es", "th");
        await seed.LinesAsync(poll.DeckId, poll.Slide, new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            ["th"] = new Dictionary<string, (string Text, string From)> { ["/question"] = ("คำถาม", PollSeed.QUESTION) },
        });

        var body = await Client.GetFromJsonAsync<JsonElement>($"/api/join/{poll.Code}?language=th");

        var translations = body.GetProperty("translations");
        translations.GetProperty("primary").GetString().ShouldBe("en");
        var languages = translations.GetProperty("languages").EnumerateArray().ToList();
        languages.Select(l => l.GetProperty("code").GetString()).ShouldBe(["en", "es", "th"]);
        languages[0].GetProperty("name").GetString().ShouldBe("English");
        languages[0].GetProperty("complete").GetInt32().ShouldBe(100);
        languages[1].GetProperty("englishName").GetString().ShouldBe("Spanish");
        languages[1].GetProperty("complete").GetInt32().ShouldBe(0);
        languages[2].GetProperty("name").GetString().ShouldBe("ไทย");
        languages[2].GetProperty("complete").GetInt32().ShouldBeInRange(1, 99);

        using var sample = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "guest.json")));
        JsonShape.AssertMatches(translations, sample.RootElement.GetProperty("live").GetProperty("translations"), "guest.json live translations");
    }

    [Test]
    public async Task GuestView_WhenOff_ShouldHaveNullTranslations()
    {
        var poll = await SeedPollAsync();

        var body = await Client.GetFromJsonAsync<JsonElement>($"/api/join/{poll.Code}");

        body.GetProperty("translations").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
