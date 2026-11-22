using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-50: a person makes a template in the editor, publishes it, shares it and starts a deck on it, and a viewer of that
// deck who has no access to the template still sees the deck in its font. Every step goes through the screens; the font
// is the committed Inter 400 fixture under a family no built-in uses.
public partial class Sw50TemplateJourneyTests : AcceptanceTest
{
    private const string FAMILY = "Journey Sans";
    private const string FRAMES = "() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))";
    private const string NEW_DECK_TEMPLATES = """
        el => [...el.querySelectorAll('[data-testid^="new-deck-template-"]')].map(o => o.dataset.testid.slice('new-deck-template-'.length))
        """;
    private const string HEAD_FONT = "el => getComputedStyle(el).getPropertyValue('--sw-font-head')";
    private const string FACE_LOADED = """
        family => [...document.fonts].some(f => f.family.replace(/["']/g, '') === family && f.status === 'loaded')
            && document.fonts.check(`16px "${family}"`)
        """;

    [Test]
    public async Task Person_WhenPublishingATemplate_ShouldSeeItInNewDeck()
    {
        var name = $"Journey Studio {Guid.NewGuid():N}"[..28];
        var templates = new TemplatesPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);

        var id = await MakeAndPublishAsync(name);

        await templates.GotoAsync();
        var row = templates.Row("mine", name);
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row.GetByTestId("template-status")).ToHaveAttributeAsync("data-status", "published");
        await Expect(row.GetByTestId("template-status")).ToHaveAttributeAsync("data-version", "1");

        await using var alexContext = await Browser.NewContextAsync(ContextOptions());
        var alexPage = await alexContext.NewPageAsync();
        await SignIn.AsAsync(alexPage, TestUser.Alex);
        await alexPage.GotoAsync(DecksPage.PATH);
        var alexDialog = await new DecksPage(alexPage).OpenNewDeckAsync();
        await Expect(alexDialog.Template("talk")).ToBeVisibleAsync();
        await alexPage.EvaluateAsync(FRAMES);
        (await alexDialog.Root.EvaluateAsync<string[]>(NEW_DECK_TEMPLATES)).ShouldNotContain(id);

        await Page.GotoAsync(TemplateEditorPage.PathFor(id));
        var editor = new TemplateEditorPage(Page);
        await Expect(editor.Status).ToHaveTextAsync("Published · v1");
        var share = new TemplateShareDialog(Page);
        await editor.Share.ClickAsync();
        await share.ChooseAsync("named");
        await share.AddEmailAsync(TestUser.Alex.Email);
        await Expect(share.Chips).ToHaveCountAsync(1);
        var saved = await Eventually.ReadAsync(() => SharingAsync(Page, id), s => s.Viewers.Contains(TestUser.Alex.Email));
        saved.Level.ShouldBe("named");
        saved.Viewers.ShouldBe([TestUser.Alex.Email]);
        await share.Done.ClickAsync();
        await Expect(share.Root).ToHaveCountAsync(0);
        await Expect(editor.Share).ToBeFocusedAsync();

        var samDecks = new DecksPage(Page);
        await samDecks.GotoAsync();
        var samDialog = await samDecks.OpenNewDeckAsync();
        await Expect(samDialog.Template(id)).ToBeVisibleAsync();
        var order = await samDialog.Root.EvaluateAsync<string[]>(NEW_DECK_TEMPLATES);
        order.ShouldContain("talk");
        Array.IndexOf(order, id).ShouldBeLessThan(Array.IndexOf(order, "talk"));

        await alexPage.ReloadAsync();
        var alexAfter = await new DecksPage(alexPage).OpenNewDeckAsync();
        await Expect(alexAfter.Template(id)).ToBeVisibleAsync();
        (await alexAfter.Root.EvaluateAsync<string[]>(NEW_DECK_TEMPLATES)).ShouldContain(id);
        await alexAfter.Cancel.ClickAsync();

        var alexTemplates = new TemplatesPage(alexPage);
        await alexTemplates.GotoAsync();
        await Expect(alexTemplates.Row("shared", name)).ToHaveCountAsync(1);
        await Expect(alexTemplates.Row("mine", name)).ToHaveCountAsync(0);

        await alexPage.GotoAsync(TemplatesPage.PublicPath(id));
        await Expect(alexTemplates.PublicName).ToHaveTextAsync(name);
        await alexTemplates.PublicUse.ClickAsync();
        var useDialog = new NewDeckDialog(alexPage);
        await Expect(useDialog.Root).ToBeVisibleAsync();
        await Expect(useDialog.Template(id)).ToBeCheckedAsync();
        var title = $"On {FAMILY} {Guid.NewGuid():N}"[..30];
        await useDialog.FillTitleAsync(title);
        await useDialog.CreateAsync();

        var deckPage = new DeckPage(alexPage);
        await Expect(alexPage).ToHaveURLAsync(DeckPageUrl());
        await Expect(deckPage.Title).ToHaveTextAsync(title);
        await Expect(deckPage.ActivityRows.Filter(new LocatorFilterOptions { HasText = "created the deck" }))
            .ToContainTextAsync($"from the {name} template with the");
        var deckId = DeckPageUrl().Match(alexPage.Url).Groups["id"].Value;

        await AgentSeed.AddQuotesAsync(TestUser.Alex, deckId, "Small batches ship.");
        var player = new PlayerPage(alexPage);
        await player.GotoAsync(deckId);
        await Expect(player.Slide(1)).ToBeVisibleAsync();
        await player.FontsReadyAsync();
        await AssertDrawnInFontAsync(alexPage);
    }

    [Test]
    public async Task Viewer_WhenTheTemplateIsPrivate_ShouldStillSeeTheDeckInItsFont()
    {
        var name = $"Private Studio {Guid.NewGuid():N}"[..28];
        await SignIn.AsAsync(Page, TestUser.Sam);
        var id = await MakeAndPublishAsync(name);
        var decks = new DecksPage(Page);
        await decks.GotoAsync();
        var dialog = await decks.OpenNewDeckAsync();
        var title = $"Private deck {Guid.NewGuid():N}"[..30];
        await dialog.FillTitleAsync(title);
        await dialog.ChooseTemplateAsync(id);
        await dialog.CreateAsync();
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        var deck = new DeckPage(Page);
        await Expect(deck.Title).ToHaveTextAsync(title);
        var sharing = new SharingDialog(Page);
        await deck.Share.ClickAsync();
        await sharing.ChooseAsync("link");
        await Expect(sharing.Link).ToHaveTextAsync(new Regex($"^{Regex.Escape(AcceptanceApp.BaseUrl)}/d/"));
        var link = await sharing.Link.TextContentAsync() ?? string.Empty;
        await sharing.Save.ClickAsync();
        await Expect(sharing.Root).ToHaveCountAsync(0);
        await Expect(deck.SharingLevel).ToHaveTextAsync("Anyone with the link");
        (await SharingAsync(Page, id)).Level.ShouldBe("private");
        await AgentSeed.AddQuotesAsync(TestUser.Sam, DeckPageUrl().Match(Page.Url).Groups["id"].Value, "Small batches ship.");

        await using var anonymous = await Browser.NewContextAsync(ContextOptions());
        var guest = await anonymous.NewPageAsync();
        var player = new PlayerPage(guest);
        var fontServed = await guest.RunAndWaitForResponseAsync(
            async () => await guest.GotoAsync(link), r => r.Url.Contains($"/api/templates/{id}/assets/"));
        fontServed.Status.ShouldBe(200);
        await Expect(player.Slide(1)).ToBeVisibleAsync();
        await player.FontsReadyAsync();
        await AssertDrawnInFontAsync(guest);

        var strangerView = new TemplatesPage(guest);
        await guest.GotoAsync(TemplatesPage.PublicPath(id));
        await Expect(strangerView.PublicMessage).ToContainTextAsync("private");
        await guest.EvaluateAsync(FRAMES);
        await Expect(strangerView.PublicName).ToHaveCountAsync(0);
        (await guest.ContentAsync()).ShouldNotContain(name);

        var ownerView = new TemplatesPage(Page);
        await Page.GotoAsync(TemplatesPage.PublicPath(id));
        await Expect(ownerView.PublicName).ToHaveTextAsync(name);

        await using var alexContext = await Browser.NewContextAsync(ContextOptions());
        var alexPage = await alexContext.NewPageAsync();
        await SignIn.AsAsync(alexPage, TestUser.Alex);
        var alexGet = await alexPage.APIRequest.GetAsync($"/api/templates/{id}");
        alexGet.Status.ShouldBe(404, await alexGet.TextAsync());
        var samGet = await Page.APIRequest.GetAsync($"/api/templates/{id}");
        samGet.Status.ShouldBe(200, await samGet.TextAsync());
    }

    // The player draws in the template's face: the frame's head font starts with its family, and that face is loaded.
    private static async Task AssertDrawnInFontAsync(IPage page)
    {
        var head = await page.GetByTestId("slide-frame").EvaluateAsync<string>(HEAD_FONT);
        head.Trim().ShouldStartWith($"'{FAMILY}'");
        var loaded = await Eventually.ReadAsync(() => page.EvaluateAsync<bool>(FACE_LOADED, FAMILY), ok => ok);
        loaded.ShouldBeTrue();
    }

    // Sam (signed in on Page) makes a template from talk, ticks the rights box, uploads the font as the heading face and
    // publishes version 1. Returns the template's id.
    private async Task<string> MakeAndPublishAsync(string name)
    {
        var templates = new TemplatesPage(Page);
        var editor = new TemplateEditorPage(Page);
        await templates.GotoAsync();
        await Expect(templates.Group("builtIn")).ToBeVisibleAsync();
        await templates.CreateAsync(name, "talk");
        await Expect(Page).ToHaveURLAsync(EditorUrl());
        var id = EditorUrl().Match(Page.Url).Groups["id"].Value;
        await Expect(editor.Status).ToHaveTextAsync("Draft");

        await editor.Tab("fonts").ClickAsync();
        await Expect(editor.FontsTab).ToBeVisibleAsync();
        await editor.Family("head").FillAsync(FAMILY);
        await editor.Family("head").BlurAsync();
        await Expect(editor.AddFiles).ToBeDisabledAsync();
        await editor.RightsTick.CheckAsync();
        await Expect(editor.AddFiles).ToBeEnabledAsync();
        await editor.FileInput.SetInputFilesAsync(FontFixtures.NormalPath);
        await Expect(editor.ToAdd).ToHaveCountAsync(1);
        await Expect(editor.ToAddFamily).ToHaveValueAsync(FAMILY);
        await Expect(editor.ToAddWeight).ToHaveValueAsync("400");
        await editor.AddToTemplate.ClickAsync();
        await Expect(editor.ToAdd).ToHaveCountAsync(0);

        // One weight (400) with no bold makes "fonts" a todo, not a fail: Publish stays on.
        var checklist = await Eventually.ReadAsync(editor.ChecklistAsync, c => c.GetValueOrDefault("fonts") == "todo" && c.GetValueOrDefault("fontRights") == "pass");
        checklist.GetValueOrDefault("fontRights").ShouldBe("pass", JsonSerializer.Serialize(checklist));
        checklist["fonts"].ShouldBe("todo");
        checklist.Values.ShouldNotContain("fail");
        await Expect(editor.Publish).ToBeEnabledAsync();
        await editor.Publish.ClickAsync();
        await Expect(editor.Status).ToHaveTextAsync("Published · v1");
        return id;
    }

    private static async Task<Sharing> SharingAsync(IPage page, string id)
    {
        var response = await page.APIRequest.GetAsync($"/api/templates/{id}/sharing");
        response.Status.ShouldBe(200, await response.TextAsync());
        var json = JsonDocument.Parse(await response.TextAsync()).RootElement;
        return new Sharing(json.GetProperty("level").GetString()!, [.. json.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()!)]);
    }

    private sealed record Sharing(string Level, IReadOnlyList<string> Viewers);

    [GeneratedRegex(@"/templates/(?<id>tpl_[0-9A-Za-z]+)/edit$")]
    private static partial Regex EditorUrl();

    [GeneratedRegex(@"/decks/(?<id>[0-9A-Za-z]{12})$")]
    private static partial Regex DeckPageUrl();
}
