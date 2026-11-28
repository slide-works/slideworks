using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-883's guest side on the real app, the real hub and the real presentation registry. Sam's deck goes in over MCP and is
// translated into Thai over MCP (set_translations and translate_slides, nothing else seeds it). Each guest is a browser
// context at 390×844 that opens the join link the server gave; a phone whose own language is Thai starts in Thai.
public partial class Sw883GuestLanguageJourneyTests : AcceptanceTest
{
    private const string QUOTE = "Small batches ship sooner";
    private const string QUOTE_THAI = "ชุดเล็กส่งเร็วกว่า";
    private const string ATTRIBUTION = "Sam Rivera";
    private const string ATTRIBUTION_THAI = "แซม ริเวร่า";
    private const string QUESTION = "How big was your last change?";
    private const string QUESTION_THAI = "การเปลี่ยนแปลงครั้งล่าสุดของคุณใหญ่แค่ไหน";
    private static readonly string[] _options = ["A few lines", "A few files", "A whole feature"];
    private static readonly string[] _optionsThai = ["ไม่กี่บรรทัด", "ไม่กี่ไฟล์", "ทั้งฟีเจอร์"];
    private const int POLL = 2;
    private const int POLL_SLIDES = 3;
    private const float PUSH_TIMEOUT = 15_000;

    // A phone in Thai opens the join link: the slide is in Thai, the chip says so, the banner names Thai, and the guest
    // switches to English from the chip's sheet.
    [Test]
    public async Task Guest_WhenPhoneIsThai_ShouldStartInThaiAndSwitchToEnglish()
    {
        var deckId = await GuestLanguageSeed.CreateDeckAsync("Thai phone", async (agent, id) =>
        {
            await agent.AddSlideAsync(id, "quote", new { quote = QUOTE, attribution = ATTRIBUTION });
            await agent.OfferThaiAsync(id);
            await agent.TranslateToThaiAsync(id, 1, new() { ["/quote"] = QUOTE_THAI, ["/attribution"] = ATTRIBUTION_THAI });
        });
        var joinUrl = await GoLiveAsync(deckId);

        await using var phone = await GuestLanguageSeed.OpenPhoneContextAsync(Browser, "th-TH");
        var guest = new GuestPage(await phone.NewPageAsync());
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();

        await Expect(guest.Slide).ToContainTextAsync(QUOTE_THAI, new() { UseInnerText = true });
        await Expect(guest.Slide).Not.ToContainTextAsync(QUOTE, new() { UseInnerText = true });
        await Expect(guest.LanguageChip).ToHaveAttributeAsync("aria-label", "Read the slides in Thai");
        await Expect(guest.LanguageBanner).ToContainTextAsync("Showing the slides in ไทย");
        await Expect(guest.FallbackNote).ToHaveCountAsync(0);

        await guest.LanguageChip.ClickAsync();
        await Expect(guest.LanguageSheet).ToBeVisibleAsync();
        await Expect(guest.LanguageOptions).ToHaveCountAsync(2);
        await Expect(guest.LanguageOption("ไทย")).ToHaveAttributeAsync("aria-pressed", "true");
        await guest.LanguageOption("English").ClickAsync();

        await Expect(guest.LanguageSheet).ToHaveCountAsync(0);
        await Expect(guest.Slide).ToContainTextAsync(QUOTE, new() { UseInnerText = true });
        await Expect(guest.Slide).Not.ToContainTextAsync(QUOTE_THAI, new() { UseInnerText = true });
        await Expect(guest.LanguageChip).ToHaveAttributeAsync("aria-label", "Read the slides in English");
        await Expect(guest.FallbackNote).ToHaveCountAsync(0);
    }

    // Only the quote is translated: the attribution is missing, so it shows in English beside the Thai quote, and the
    // phone says one line is in English.
    [Test]
    public async Task Guest_WhenLineMissing_ShouldShowPrimaryAndNote()
    {
        var deckId = await GuestLanguageSeed.CreateDeckAsync("Missing line", async (agent, id) =>
        {
            await agent.AddSlideAsync(id, "quote", new { quote = QUOTE, attribution = ATTRIBUTION });
            await agent.OfferThaiAsync(id);
            await agent.TranslateToThaiAsync(id, 1, new() { ["/quote"] = QUOTE_THAI });
        });
        var joinUrl = await GoLiveAsync(deckId);

        await using var phone = await GuestLanguageSeed.OpenPhoneContextAsync(Browser, "th-TH");
        var guest = new GuestPage(await phone.NewPageAsync());
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();

        await Expect(guest.Slide).ToContainTextAsync(QUOTE_THAI, new() { UseInnerText = true });
        await Expect(guest.Slide).ToContainTextAsync(ATTRIBUTION, new() { UseInnerText = true });
        await Expect(guest.FallbackNote).ToHaveTextAsync("1 line on this slide is in English");
    }

    // One guest answers in Thai and one in English: both land on the same option, and the big screen, which is not a
    // guest, counts them on one bar and keeps the primary labels.
    [Test]
    public async Task Poll_WhenGuestsAnswerInThaiAndEnglish_ShouldCountOnSameBar()
    {
        var deckId = await GuestLanguageSeed.CreateDeckAsync("Poll languages", async (agent, id) =>
        {
            await agent.AddSlideAsync(id, "join", new { });
            await agent.AddSlideAsync(id, "poll", new { question = QUESTION, options = _options });
            await agent.AddSlideAsync(id, "quote", new { quote = QUOTE, attribution = ATTRIBUTION });
            await agent.OfferThaiAsync(id);
            await agent.TranslateToThaiAsync(id, POLL, new()
            {
                ["/question"] = QUESTION_THAI,
                ["/options/0"] = _optionsThai[0],
                ["/options/1"] = _optionsThai[1],
                ["/options/2"] = _optionsThai[2],
            });
        });
        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        await Expect(presenter.Connecting).ToHaveCountAsync(0);
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {POLL_SLIDES}");
        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.End).ToBeVisibleAsync();
        var joinRead = await PollJourney.ReadAsync(Page, $"/api/decks/{deckId}/live", "joinUrl");
        joinRead.Status.ShouldBe(200, joinRead.Body);
        var audienceTab = await Context.NewPageAsync();
        var audience = await Talks.OpenAudienceAsync(Page, audienceTab, presenter, deckId);
        await Page.BringToFrontAsync();
        var screen = new PollSlide(audience.Slide(POLL));

        await using var thaiContext = await GuestLanguageSeed.OpenPhoneContextAsync(Browser, "th-TH");
        var thai = await Talks.FollowAsync(await thaiContext.NewPageAsync(), joinRead.Value.ShouldNotBeNull());
        var thaiCard = new GuestPollCard(thai.Root.Page);
        await using var englishContext = await GuestLanguageSeed.OpenPhoneContextAsync(Browser, "en-US");
        var english = await Talks.FollowAsync(await englishContext.NewPageAsync(), joinRead.Value);
        var englishCard = new GuestPollCard(english.Root.Page);

        await Talks.MoveToAsync(presenter, audience, POLL_SLIDES, "ArrowRight", POLL, PUSH_TIMEOUT);

        await Expect(thaiCard.Question).ToHaveTextAsync(QUESTION_THAI, new() { Timeout = PUSH_TIMEOUT });
        await Expect(thaiCard.Option(0)).ToHaveAccessibleNameAsync(OptionName(_optionsThai[0]));
        await Expect(englishCard.Question).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });
        await Expect(englishCard.Option(0)).ToHaveAccessibleNameAsync(OptionName(_options[0]));
        await Expect(screen.Question).ToHaveTextAsync(QUESTION, new() { Timeout = PUSH_TIMEOUT });

        await thaiCard.Option(0).ClickAsync();
        await Expect(thaiCard.Sent).ToBeVisibleAsync();
        await englishCard.Option(0).ClickAsync();
        await Expect(englishCard.Sent).ToBeVisibleAsync();

        await Expect(screen.Answered).ToHaveTextAsync("2 answers", new() { Timeout = PUSH_TIMEOUT });
        await Expect(screen.Percent(0)).ToHaveTextAsync("100%");
        await Expect(screen.Percent(1)).ToHaveTextAsync("0%");
        for (var position = 0; position < _options.Length; position++)
        {
            await Expect(screen.Row(position)).ToContainTextAsync(_options[position]);
            await Expect(screen.Row(position)).Not.ToContainTextAsync(Thai());
        }

        await Expect(screen.Question).Not.ToContainTextAsync(Thai());
        await Expect(thaiCard.Answered).ToHaveTextAsync("2 answers so far", new() { Timeout = PUSH_TIMEOUT });
        await Expect(thaiCard.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(thaiCard.OptionYou(0)).ToBeVisibleAsync();
        await Expect(englishCard.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(thaiCard.Option(0)).ToHaveAccessibleNameAsync(OptionName(_optionsThai[0]));
        await Expect(englishCard.Option(0)).ToHaveAccessibleNameAsync(OptionName(_options[0]));
    }

    // Opens a live session the way the owner's page does, over the API, and returns the join link. The owner's player page
    // stays open so the presenter's position is published.
    private async Task<string> GoLiveAsync(string deckId)
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync("1 / 1");
        await player.LiveChip.GoLive.ClickAsync();
        await Expect(player.LiveChip.End).ToBeVisibleAsync();
        var joinRead = await PollJourney.ReadAsync(Page, $"/api/decks/{deckId}/live", "joinUrl");
        joinRead.Status.ShouldBe(200, joinRead.Body);
        return joinRead.Value.ShouldNotBeNull();
    }

    // The option's radio carries its text as its accessible name (the option, then the question).
    private static Regex OptionName(string option) => new(Regex.Escape(option));

    [GeneratedRegex("[฀-๿]")]
    private static partial Regex Thai();
}
