using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-50: the owner's half of the custom-layout journey. Sam makes a template from talk, uploads the five fixture layouts
// in the editor, reads each review page, approves each and publishes; an agent on a deck of that template then lists the
// layouts. Every step goes through the screens, the MCP tool is read as an agent reads it.
public partial class Sw50CustomLayoutOwnerJourneyTests : AcceptanceTest
{
    private const string FRAMES = "() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))";

    private static readonly string[] NAMES = ["agent-lanes", "code-mock", "chat-mock", "dial", "horizon"];

    [Test]
    public async Task Owner_WhenApprovingAllLayouts_ShouldPublish()
    {
        var name = $"Layouts Studio {Guid.NewGuid():N}"[..28];
        var templates = new TemplatesPage(Page);
        var editor = new TemplateEditorPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await templates.GotoAsync();
        await Expect(templates.Group("builtIn")).ToBeVisibleAsync();
        await templates.CreateAsync(name, "talk");
        await Expect(Page).ToHaveURLAsync(EditorUrl());
        var id = EditorUrl().Match(Page.Url).Groups["id"].Value;
        await Expect(editor.Status).ToHaveTextAsync("Draft");

        var tab = await editor.OpenLayoutsAsync();
        for (var uploaded = 0; uploaded < NAMES.Length; uploaded++)
        {
            await tab.Files.SetInputFilesAsync(Fixture.Files(NAMES[uploaded]));
            await Expect(tab.Rows).ToHaveCountAsync(uploaded + 1);
            await Expect(tab.Row(NAMES[uploaded])).ToHaveCountAsync(1);
        }
        await Expect(tab.Rows.First).ToBeVisibleAsync();
        (await tab.NamesAsync()).Order(StringComparer.Ordinal).ShouldBe(NAMES.Order(StringComparer.Ordinal));
        foreach (var layout in NAMES)
            await Expect(tab.StatusOf(layout)).ToHaveTextAsync("Needs review");

        // A sixth layout whose markup holds a script is refused with the line of the script and no echo of it; the tab
        // still lists the five (the same list that showed the accepted ones, once the page has settled).
        var sixth = Fixture.WithScript("dial", "sixth");
        System.Text.Encoding.UTF8.GetString(sixth.Single(f => f.Name == "slots.json").Buffer).ShouldContain("\"name\": \"sixth\"");
        await tab.Files.SetInputFilesAsync(sixth);
        await Expect(tab.Refusals).ToBeVisibleAsync();
        var findings = await tab.FindingsAsync();
        findings.ShouldNotBeEmpty();
        findings.ShouldContain(f => f.Path == Fixture.SCRIPT_PATH);
        findings.ShouldAllBe(f => !f.Message.Contains("<script", StringComparison.OrdinalIgnoreCase) && !f.Message.Contains("alert(", StringComparison.Ordinal));
        await Page.EvaluateAsync(FRAMES);
        await Expect(tab.Rows.First).ToBeVisibleAsync();
        await Expect(tab.Rows).ToHaveCountAsync(5);
        var shown = await Eventually.ReadAsync(tab.NamesAsync, n => n.Count == 5);
        shown.ShouldNotContain("sixth");
        shown.ShouldContain("dial");
        shown.Count.ShouldBe(5);

        // Publishing is closed while a layout is unapproved: the button is off, the checklist row fails, the route
        // refuses with its code, and the Templates page still shows a draft.
        await Expect(editor.Publish).ToBeDisabledAsync();
        var before = await Eventually.ReadAsync(editor.ChecklistAsync, c => c.GetValueOrDefault("layouts") == "fail");
        before.GetValueOrDefault("layouts").ShouldBe("fail", JsonSerializer.Serialize(before));
        var refused = await Page.APIRequest.PostAsync($"/api/templates/{id}/publish", new() { Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl } });
        refused.Status.ShouldBe(400, await refused.TextAsync());
        JsonDocument.Parse(await refused.TextAsync()).RootElement.GetProperty("error").GetString().ShouldBe("layout_not_approved");
        await templates.GotoAsync();
        await Expect(templates.Row("mine", name)).ToHaveCountAsync(1);
        await Expect(TemplatesPage.StatusOf(templates.Row("mine", name))).ToHaveTextAsync("Draft");

        var review = new LayoutReviewPage(Page);
        foreach (var layout in NAMES)
            await ReviewAndApproveAsync(review, id, layout);

        await Page.GotoAsync(TemplateEditorPage.PathFor(id));
        var again = await editor.OpenLayoutsAsync();
        await Expect(again.Rows).ToHaveCountAsync(5);
        foreach (var layout in NAMES)
            await Expect(again.StatusOf(layout)).ToHaveTextAsync("Approved");
        var after = await Eventually.ReadAsync(editor.ChecklistAsync, c => c.GetValueOrDefault("layouts") == "pass");
        after.GetValueOrDefault("layouts").ShouldBe("pass", JsonSerializer.Serialize(after));
        after.Values.ShouldAllBe(state => state == "pass", JsonSerializer.Serialize(after));
        await Expect(editor.Publish).ToBeEnabledAsync();
        await editor.Publish.ClickAsync();
        await Expect(editor.Status).ToHaveTextAsync("Published · v1");
        await templates.GotoAsync();
        await Expect(TemplatesPage.StatusOf(templates.Row("mine", name))).ToHaveTextAsync("Published · v1");

        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = (await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Layouts deck {Guid.NewGuid():N}"[..30], ["template"] = id })).GetProperty("deck").GetString().ShouldNotBeNull();
        var listed = (await AgentSeed.CallAsync(agent, "list_layouts", new() { ["deck"] = deck })).GetProperty("layouts").EnumerateArray().ToList();
        var custom = listed.Where(l => l.GetProperty("custom").GetBoolean()).ToList();
        custom.Select(l => l.GetProperty("name").GetString()).Order(StringComparer.Ordinal).ShouldBe(NAMES.Order(StringComparer.Ordinal));
        listed.Take(listed.Count - 5).ShouldNotBeEmpty("the same list holds the built-ins");
        listed.Take(listed.Count - 5).ShouldAllBe(l => !l.GetProperty("custom").GetBoolean());
        listed.Skip(listed.Count - 5).ShouldAllBe(l => l.GetProperty("custom").GetBoolean());
        foreach (var layout in custom)
            layout.GetProperty("description").GetString().ShouldBe(Fixture.DescriptionOf(layout.GetProperty("name").GetString()!));
    }

    // Opens one layout's review page, reads what the page's own measurement found, approves and checks the outcome.
    private async Task ReviewAndApproveAsync(LayoutReviewPage review, string id, string layout)
    {
        var darkToo = Fixture.SurfaceOf(layout) != "light";
        await Page.GotoAsync(LayoutReviewPage.PathFor(id, layout));
        await Expect(review.Status).ToHaveTextAsync("Needs review");
        await Expect(review.Render("sample")).ToBeVisibleAsync();
        await Expect(review.Render("maximum")).ToBeVisibleAsync();
        if (darkToo)
            await Expect(review.Render("dark")).ToBeVisibleAsync();
        var found = await Eventually.ReadAsync(() => review.Reason.InnerTextAsync(), text => !text.Contains("Measuring", StringComparison.Ordinal));
        found.ShouldBe(string.Empty, $"{layout}: the page found {found}");
        await Expect(review.Approve).ToBeEnabledAsync();

        var checks = await Eventually.ReadAsync(review.ChecksAsync, c => c.GetValueOrDefault("Renders drawn") == "Passed");
        var shown = JsonSerializer.Serialize(checks);
        checks.GetValueOrDefault("Renders drawn").ShouldBe("Passed", $"{layout}: {shown}");
        checks.GetValueOrDefault("Safe area").ShouldBe("Passed", $"{layout}: {shown}");
        checks.GetValueOrDefault("Contrast on light").ShouldBe("Passed", $"{layout}: {shown}");
        if (darkToo)
            checks.GetValueOrDefault("Contrast on dark").ShouldBe("Passed", $"{layout}: {shown}");
        else
            checks.ContainsKey("Contrast on dark").ShouldBeFalse($"{layout} is light-only: {shown}");
        await Expect(review.Render("maximum").GetByTestId("layout-review-safe-area")).ToBeVisibleAsync();
        var box = await Eventually.ReadAsync(review.SafeAreaBoxAsync, b => Math.Abs(b.Right - 1800) < 1);
        new[] { box.Left, box.Top, box.Right, box.Bottom }.Zip(new[] { 120d, 150d, 1800d, 996d }).ShouldAllBe(e => Math.Abs(e.First - e.Second) < 1, $"{layout}: the dashed box is at {box.Left},{box.Top},{box.Right},{box.Bottom}");

        await review.Approve.ClickAsync();
        await Expect(review.Status).ToHaveTextAsync("Approved");
        await Expect(review.Status).ToBeFocusedAsync();
    }

    // The fixture folders under SlideWorks.WebApi.Tests, found by walking up to the solution as FontFixtures does.
    private static class Fixture
    {
        // The line the script lands on in the sixth layout's markup.
        public const string SCRIPT_PATH = "layout.html:2";

        public static IReadOnlyList<FilePayload> Files(string name) =>
        [
            Payload("layout.html", "text/html", Read(name, "layout.html")),
            Payload("layout.css", "text/css", Read(name, "layout.css")),
            Payload("slots.json", "application/json", Read(name, "slots.json")),
        ];

        // The same files under a new name, with a script element on line 2 of the markup.
        public static IReadOnlyList<FilePayload> WithScript(string from, string name)
        {
            var lines = Read(from, "layout.html").ReplaceLineEndings("\n").Split('\n').ToList();
            lines.Insert(1, "<script>alert(1)</script>");
            var slots = Read(from, "slots.json").Replace($"\"name\": \"{from}\"", $"\"name\": \"{name}\"", StringComparison.Ordinal);
            return
            [
                Payload("layout.html", "text/html", string.Join('\n', lines)),
                Payload("layout.css", "text/css", Read(from, "layout.css")),
                Payload("slots.json", "application/json", slots),
            ];
        }

        public static string SurfaceOf(string name) =>
            JsonDocument.Parse(Read(name, "slots.json")).RootElement.GetProperty("surface").GetString()!;

        public static string DescriptionOf(string name) =>
            JsonDocument.Parse(Read(name, "slots.json")).RootElement.GetProperty("description").GetString()!;

        private static FilePayload Payload(string file, string type, string text) =>
            new() { Name = file, MimeType = type, Buffer = System.Text.Encoding.UTF8.GetBytes(text) };

        private static string Read(string name, string file)
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
                    return File.ReadAllText(Path.Combine(directory.FullName, "SlideWorks.WebApi.Tests", "Templates", "Fixtures", "custom-layouts", name, file));
            }
            throw new InvalidOperationException($"No SlideWorks.slnx above {AppContext.BaseDirectory}.");
        }
    }

    [GeneratedRegex(@"/templates/(?<id>tpl_[0-9A-Za-z]+)/edit(\?.*)?$")]
    private static partial Regex EditorUrl();
}
