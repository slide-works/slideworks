using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates.Layouts;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// The five layouts of the deck's own template (D38), written as real layout files under Fixtures/custom-layouts, taken
// through the REAL compiler and the real routes. Nothing is faked. Each folder holds layout.html, layout.css,
// slots.json and sample.json (slot contents in made-up Northwind words).
[TestFixture]
[NonParallelizable]
[CancelAfter(120000)]
public class CustomLayoutFixturesTests : ApiTestFixture
{
    private const string SCOPE = "0a1b2c3d";

    private static readonly string[] Names = ["agent-lanes", "code-mock", "chat-mock", "dial", "horizon"];

    // Steps a sample slide reveals (its list's length; 0 where the layout has none).
    private static readonly Dictionary<string, int> SampleSteps = new()
    {
        ["agent-lanes"] = 4, ["code-mock"] = 0, ["chat-mock"] = 4, ["dial"] = 0, ["horizon"] = 3,
    };

    private static readonly ICustomLayoutCompiler Compiler = new CustomLayoutCompiler();

    [TestCaseSource(nameof(Names))]
    public void Fixture_WhenCompiled_ShouldPassEveryCheck(string name)
    {
        var fixture = Fixture.Of(name);

        var result = Compiler.Compile(fixture.Markup, fixture.Css, fixture.Slots, SCOPE);

        result.Errors.ShouldBeEmpty(string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code}")));
        var layout = result.Layout.ShouldNotBeNull();
        layout.Name.ShouldBe(name);
        var classes = System.Text.RegularExpressions.Regex.Matches(layout.Css, @"\.([a-z][A-Za-z0-9_-]*)").Select(m => m.Groups[1].Value).ToList();
        classes.ShouldNotBeEmpty();
        classes.ShouldAllBe(c => c.StartsWith($"cl-{SCOPE}-{name}__", StringComparison.Ordinal));
    }

    [TestCaseSource(nameof(Names))]
    public void Fixture_WhenCompiled_ShouldKeepItsBindings(string name)
    {
        var nodes = Compile(name).Tree.Root.SelectMany(n => n.WithDescendants()).ToList();

        switch (name)
        {
            case "agent-lanes":
                nodes.ShouldContain(n => n.Each == "lanes" && n.Step == TreeNode.STEP_EACH && n.Variants.Any(v => v.Slot == "state"));
                nodes.ShouldContain(n => n.Vars.Any(v => v.Name == "--p" && v.Slot == "progress"));
                break;
            case "dial":
                nodes.ShouldContain(n => n.Vars.Any(v => v.Name == "--v" && v.Slot == "value"));
                break;
            case "horizon":
                nodes.ShouldContain(n => n.Each == "rows" && n.WithDescendants().Any(d => d != n && d.Each == "segments"));
                break;
            case "code-mock":
                nodes.ShouldContain(n => n.If == "ghost");
                break;
            default:
                nodes.ShouldContain(n => n.Variants.Any(v => v.Slot == "from"));
                break;
        }
    }

    [TestCaseSource(nameof(Names))]
    public void Sample_WhenValidatedAgainstItsDefinition_ShouldPass(string name)
    {
        var layout = Compile(name);

        Problems(layout, Fixture.Of(name).Sample).ShouldBeEmpty();
    }

    [TestCaseSource(nameof(Names))]
    public void Maximum_WhenGenerated_ShouldPass(string name)
    {
        var layout = Compile(name);

        Problems(layout, layout.MaximumJson).ShouldBeEmpty();
    }

    [Test]
    public void Slots_WhenRead_ShouldMatchTheSpecsLimits()
    {
        JsonNode Slot(string layout, params string[] path)
        {
            var node = JsonNode.Parse(Fixture.Of(layout).Slots)!["slots"]!;
            foreach (var step in path)
            {
                node = node[step] ?? node["of"]![step]!;
            }

            return node;
        }

        (Slot("agent-lanes", "lanes")["min"]!.GetValue<int>(), Slot("agent-lanes", "lanes")["max"]!.GetValue<int>()).ShouldBe((2, 4));
        (Slot("dial", "value")["min"]!.GetValue<int>(), Slot("dial", "value")["max"]!.GetValue<int>()).ShouldBe((0, 100));
        Slot("code-mock", "lines", "code")["max"]!.GetValue<int>().ShouldBe(56);
        (Slot("chat-mock", "messages")["min"]!.GetValue<int>(), Slot("chat-mock", "messages")["max"]!.GetValue<int>()).ShouldBe((1, 4));
        (Slot("horizon", "rows")["min"]!.GetValue<int>(), Slot("horizon", "rows")["max"]!.GetValue<int>()).ShouldBe((1, 3));
    }

    [TestCaseSource(nameof(Names))]
    public void Fixture_WhenScanned_ShouldHoldNoColourOrFontLiteral(string name)
    {
        var css = Fixture.Of(name).Css;

        foreach (var banned in new[] { "#", "rgb(", "hsl(", "url(", "@", "\"", "'" })
        {
            css.ShouldNotContain(banned);
        }
    }

    [TestCaseSource(nameof(Names))]
    public async Task Fixture_WhenUploadedApprovedAndUsed_ShouldReachThePlayView(string name)
    {
        var fixture = Fixture.Of(name);
        var (agent, deck) = await PublishedDeckAsync(fixture);
        await using var _ = agent;
        using var sample = JsonDocument.Parse(fixture.Sample);
        var added = await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = name, slots = sample.RootElement });

        var playResponse = await Client.GetAsync($"/api/play/{deck}");
        var playText = await playResponse.Content.ReadAsStringAsync();
        var play = JsonDocument.Parse(playText).RootElement;
        play.GetProperty("look").GetProperty("customLayouts").EnumerateArray().Select(l => l.GetProperty("name").GetString()).ShouldContain(name);
        play.GetProperty("slides")[0].GetProperty("steps").GetInt32().ShouldBe(SampleSteps[name]);
        playText.ShouldNotContain("<");
        added.GetRawText().ShouldNotContain("<");
    }

    [Test]
    public async Task Fixture_WhenTheSampleBreaksALimit_ShouldBeRefusedWithItsPath()
    {
        var fixture = Fixture.Of("dial");
        var (agent, deck) = await PublishedDeckAsync(fixture);
        await using var _ = agent;
        var broken = JsonNode.Parse(fixture.Sample)!;
        broken["value"] = 140;
        using var slots = JsonDocument.Parse(broken.ToJsonString());

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = "dial", slots = slots.RootElement });

        error.ShouldContain("dial.value");
        (await SlideCalls.SlidesAsync(agent, deck)).Count.ShouldBe(0);
    }

    // Uploads, approves and publishes the fixture in a fresh template, and makes an empty deck on it as Sam.
    private async Task<(McpTestClient Agent, string Deck)> PublishedDeckAsync(Fixture fixture)
    {
        var name = fixture.Name;
        var created = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Northwind" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var template = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var asset = await UploadAsync(template, fixture);
        var editor = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{template}");
        var manifest = JsonNode.Parse(editor.GetProperty("manifest").GetRawText())!;
        manifest["customLayouts"] = new JsonArray(new JsonObject { ["name"] = name, ["file"] = asset });
        using var put = await Client.PutAsJsonAsync($"/api/templates/{template}", new { manifest, revision = editor.GetProperty("revision").GetInt32() });
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        using var approve = await Client.PostAsJsonAsync($"/api/templates/{template}/layouts/{name}/approve", new { asset, overflow = Array.Empty<string>(), contrast = Array.Empty<string>() });
        approve.StatusCode.ShouldBe(HttpStatusCode.NoContent, await approve.Content.ReadAsStringAsync());
        using var publish = await Client.PostAsync($"/api/templates/{template}/publish", null);
        publish.StatusCode.ShouldBe(HttpStatusCode.OK, await publish.Content.ReadAsStringAsync());

        var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var (isError, made) = await agent.CallToolAsync("create_deck", new { title = "Ship smaller", template });
        isError.ShouldBeFalse(made.ToString());
        return (agent, made.GetProperty("deck").GetString()!);
    }

    [Test]
    public void Horizon_WhenCompiledWithItsSegmentFloor_ShouldPassTheRealPipelineAndHoldTheFloor()
    {
        var fixture = Fixture.Of("horizon");
        var seg = System.Text.RegularExpressions.Regex.Match(fixture.Css, @"\.seg \{[^}]*\}").Value;
        seg.ShouldMatch(@"min-width: [1-9]\d*px");
        seg.ShouldContain("overflow: hidden");

        var result = Compiler.Compile(fixture.Markup, fixture.Css, fixture.Slots, SCOPE);

        result.Errors.ShouldBeEmpty(string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code}")));
        result.Layout.ShouldNotBeNull().Css.ShouldContain("min-width");
    }

    private static CompiledLayout Compile(string name)
    {
        var fixture = Fixture.Of(name);
        var result = Compiler.Compile(fixture.Markup, fixture.Css, fixture.Slots, SCOPE);
        return result.Layout ?? throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code}")));
    }

    private static IReadOnlyList<string> Problems(CompiledLayout layout, string slotsJson)
    {
        using var slots = JsonDocument.Parse(slotsJson);
        return SlideValidator.Validate(layout.Definition, slots.RootElement, FitWidths.Default).Errors.Select(e => e.ToString()).ToList();
    }

    // The fixture as one {markup, css, slots} document through the upload routes as Sam. Returns the asset id.
    private async Task<string> UploadAsync(string template, Fixture fixture)
    {
        var body = new JsonObject { ["markup"] = fixture.Markup, ["css"] = fixture.Css, ["slots"] = JsonNode.Parse(fixture.Slots) }.ToJsonString();
        var start = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "layout", fileName = "layout.json", rightsConfirmed = false });
        start.StatusCode.ShouldBe(HttpStatusCode.Created, await start.Content.ReadAsStringAsync());
        var url = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        using var put = await http.PutAsync(url, content);
        var stored = await put.Content.ReadFromJsonAsync<JsonElement>();
        put.StatusCode.ShouldBe(HttpStatusCode.OK, stored.ToString());
        return stored.GetProperty("assetId").GetString()!;
    }

    private sealed record Fixture(string Name, string Markup, string Css, string Slots, string Sample)
    {
        public static Fixture Of(string name)
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
            {
                directory = directory.Parent;
            }

            var folder = Path.Combine(directory!.FullName, "SlideWorks.WebApi.Tests", "Templates", "Fixtures", "custom-layouts", name);
            string Read(string file) => File.ReadAllText(Path.Combine(folder, file));
            return new Fixture(name, Read("layout.html"), Read("layout.css"), Read("slots.json"), Read("sample.json"));
        }
    }
}
