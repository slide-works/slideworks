using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates.Layouts;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// The custom-layout seams (T10) as they stand before the store and the compiler stages are built: what the registered
// classes answer, the stored form, the two fakes, and the samples the two sides of wave 2 build against.
[TestFixture]
public class TemplateLayoutsContractTests : ApiTestFixture
{
    private const string SAMPLE_NAME = "bar-chart";
    private const string SAMPLE_SCOPE = "0a1b2c3d";

    [Test]
    public async Task ForVersion_WhenNothingIsBuilt_ShouldBeEmpty()
    {
        using var scope = Factory.Services.CreateScope();
        var layouts = scope.ServiceProvider.GetRequiredService<ITemplateLayouts>();

        var draft = await layouts.ForVersionAsync("tpl_layouts00001", 0, CancellationToken.None);
        var published = await layouts.ForVersionAsync("tpl_layouts00001", 1, CancellationToken.None);

        layouts.ShouldBeOfType<TemplateLayouts>();
        draft.ShouldBeEmpty();
        published.ShouldBeEmpty();
    }

    [Test]
    public async Task Approve_WhenNothingIsBuilt_ShouldBeNotFound()
    {
        using var scope = Factory.Services.CreateScope();
        var layouts = scope.ServiceProvider.GetRequiredService<ITemplateLayouts>();

        var outcome = await layouts.ApproveAsync("tpl_layouts00002", SAMPLE_NAME, new LayoutReport("0a1b2c3d4e5f60718293a4b5c6d7e8f9", [], []), CancellationToken.None);

        outcome.ShouldBe(ApproveOutcome.NotFound);
    }

    [Test]
    public void Stored_WhenRoundTripped_ShouldBeUnchanged()
    {
        var compiler = SampleCompiler();
        var meta = new JsonObject
        {
            ["layout"] = JsonNode.Parse(CompiledSample()),
            ["review"] = new JsonObject
            {
                ["approved"] = true,
                ["overflow"] = new JsonArray("headline"),
                ["contrast"] = new JsonArray("muted on chip"),
            },
        }.ToJsonString();

        var stored = StoredLayout.Read(meta, compiler);
        var written = stored.Write();

        stored.Approved.ShouldBeTrue();
        stored.Overflow.ShouldBe(["headline"]);
        stored.Contrast.ShouldBe(["muted on chip"]);
        stored.Layout.CompilerVersion.ShouldBe(CompiledLayout.COMPILER_VERSION);
        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(meta)).ShouldBeTrue(written);
    }

    [Test]
    public void Stored_WhenNew_ShouldBeUnapprovedWithEmptyLists()
    {
        var stored = StoredLayout.New(SampleCompiler().Read(CompiledSample()));

        var review = JsonNode.Parse(stored.Write())!["review"]!;

        review.ToJsonString().ShouldBe("""{"approved":false,"overflow":[],"contrast":[]}""");
    }

    // A row that is not exactly the stored shape is never read as a layout, approved or not.
    [TestCase("""{"layout":LAYOUT}""")]
    [TestCase("""{"layout":LAYOUT,"review":{"approved":true,"overflow":[],"contrast":[]},"extra":1}""")]
    [TestCase("""{"layout":LAYOUT,"review":{"approved":"true","overflow":[],"contrast":[]}}""")]
    [TestCase("""{"layout":LAYOUT,"review":{"approved":true,"overflow":[1],"contrast":[]}}""")]
    [TestCase("""{"layout":LAYOUT,"review":{"approved":true,"overflow":[]}}""")]
    [TestCase("""{"layout":LAYOUT,"review":{"approved":false,"approved":true,"overflow":[],"contrast":[]}}""")]
    [TestCase("""[]""")]
    public void Stored_WhenNotTheStoredShape_ShouldThrowJsonException(string meta)
    {
        var compiler = SampleCompiler();

        Should.Throw<JsonException>(() => StoredLayout.Read(meta.Replace("LAYOUT", CompiledSample(), StringComparison.Ordinal), compiler));
    }

    // A class without the layout's own prefix is refused through the seam, as Core refuses it.
    [Test]
    public void Stored_WhenAClassIsNotTheLayoutsOwn_ShouldBeRefused()
    {
        var compiler = SampleCompiler();
        var foreign = CompiledSample().Replace("cl-0a1b2c3d-bar-chart__note", "cl-ffffffff-bar-chart__note", StringComparison.Ordinal);
        var meta = """{"layout":LAYOUT,"review":{"approved":true,"overflow":[],"contrast":[]}}""".Replace("LAYOUT", foreign, StringComparison.Ordinal);

        Should.Throw<StoredLayoutRefusedException>(() => StoredLayout.Read(meta, compiler));
    }

    [Test]
    public void Compiler_WhenResolved_ShouldBeTheRealClass()
    {
        var compiler = Factory.Services.GetRequiredService<ICustomLayoutCompiler>();

        compiler.ShouldBeOfType<CustomLayoutCompiler>();
        Factory.Services.GetRequiredService<ICustomLayoutCompiler>().ShouldBeSameAs(compiler);
    }

    [Test]
    public void Read_WhenGivenTheCompiledSample_ShouldKeepNameTreeAndCss()
    {
        var compiler = SampleCompiler();
        using var sample = JsonDocument.Parse(CompiledSample());

        var layout = compiler.Read(CompiledSample());

        layout.Name.ShouldBe(SAMPLE_NAME);
        layout.Tree.Scope.ShouldBe(SAMPLE_SCOPE);
        layout.Css.ShouldBe(sample.RootElement.GetProperty("css").GetString());
        layout.Definition.ShouldBeSameAs(LayoutCatalogue.Find("bullets"));
        using var written = JsonDocument.Parse(CompiledLayoutJson.Write(layout));
        JsonElement.DeepEquals(written.RootElement.GetProperty("tree"), sample.RootElement.GetProperty("tree")).ShouldBeTrue();
    }

    [Test]
    public void Read_WhenNoDefinitionWasHandedIn_ShouldThrowInvalidOperation()
    {
        var compiler = new FakeCustomLayoutCompiler();

        Should.Throw<InvalidOperationException>(() => compiler.Read(CompiledSample()));
    }

    [Test]
    public void Limits_WhenCompared_ShouldEqualTemplateLimits()
    {
        CustomLayoutLimits.MAX_UPLOAD_BYTES.ShouldBe(TemplateLimits.MAX_LAYOUT_BYTES);
        CustomLayoutLimits.MAX_UPLOAD_BYTES.ShouldBe(800_000);
        CustomLayoutLimits.MAX_PART_BYTES.ShouldBe(TemplateLimits.MAX_LAYOUT_PART_BYTES);
        CustomLayoutLimits.MAX_PART_BYTES.ShouldBe(262_144);
    }

    [Test]
    public void ReviewSample_WhenCompared_ShouldHoldTheCompiledSamplesTreeCssAndSlots()
    {
        using var compiled = JsonDocument.Parse(CompiledSample());
        using var review = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), TemplateScreenShapes.LAYOUT_REVIEW_FILE)));
        var sample = compiled.RootElement;
        var layout = review.RootElement.GetProperty("layout");

        JsonElement.DeepEquals(layout.GetProperty("tree"), sample.GetProperty("tree")).ShouldBeTrue();
        layout.GetProperty("css").GetString().ShouldBe(sample.GetProperty("css").GetString());
        JsonElement.DeepEquals(layout.GetProperty("slots"), sample.GetProperty("schema").GetProperty("slots")).ShouldBeTrue();
        layout.GetProperty("name").GetString().ShouldBe(sample.GetProperty("name").GetString());
        review.RootElement.GetProperty("name").GetString().ShouldBe(sample.GetProperty("name").GetString());
        review.RootElement.GetProperty("surface").GetString().ShouldBe(sample.GetProperty("surface").GetString());
        JsonElement.DeepEquals(review.RootElement.GetProperty("sample"), sample.GetProperty("sample")).ShouldBeTrue();
        JsonElement.DeepEquals(review.RootElement.GetProperty("maximum"), sample.GetProperty("maximum")).ShouldBeTrue();
    }

    // The rule every reader of a stored layout checks: the tree's scope is the first 8 characters of the layout asset's
    // id. The review sample obeys it, and its steps are the compiled sample's own count.
    [Test]
    public void ReviewSample_WhenCompared_ShouldHaveAnAssetIdThatStartsWithItsTreesScope()
    {
        using var compiled = JsonDocument.Parse(CompiledSample());
        using var review = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), TemplateScreenShapes.LAYOUT_REVIEW_FILE)));
        var asset = review.RootElement.GetProperty("asset").GetString()!;
        var scope = review.RootElement.GetProperty("layout").GetProperty("tree").GetProperty("scope").GetString()!;

        LayoutScope.IsValid(scope).ShouldBeTrue();
        asset.Length.ShouldBe(32);
        asset.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')).ShouldBeTrue();
        asset[..LayoutScope.LENGTH].ShouldBe(scope);
        review.RootElement.GetProperty("steps").GetInt32().ShouldBe(compiled.RootElement.GetProperty("steps").GetProperty("count").GetInt32());
    }

    [Test]
    public void Fake_WhenCompileIsCalled_ShouldRecordTheScope()
    {
        var compiler = SampleCompiler();
        var layout = compiler.Read(CompiledSample());
        var error = CustomLayoutError.Of("layout.css:3", CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE);

        compiler.LastScope.ShouldBeNull();
        var unset = compiler.Compile("<div></div>", "", "{}", "0a1b2c3d");
        compiler.Accept(layout);
        var accepted = compiler.Compile("<div></div>", "", "{}", "11aa22bb");
        compiler.Refuse(error);
        var refused = compiler.Compile("<div></div>", "", "{}", "deadbeef");

        unset.Layout.ShouldBeNull();
        unset.Errors.Count.ShouldBe(1);
        accepted.Layout.ShouldBeSameAs(layout);
        accepted.Errors.ShouldBeEmpty();
        refused.Layout.ShouldBeNull();
        refused.Errors.ShouldBe([error]);
        compiler.LastScope.ShouldBe("deadbeef");
    }

    // The scope goes into every class the server writes, so the fake holds it to the pipeline's rule.
    [TestCase("")]
    [TestCase("0A1B2C3D")]
    [TestCase("0a1b2c3")]
    [TestCase("0a1b2c3d4")]
    [TestCase("0a1b2c3g")]
    public void Fake_WhenTheScopeIsNotEightLowerHex_ShouldThrowAndRecordNothing(string scope)
    {
        var compiler = new FakeCustomLayoutCompiler();

        Should.Throw<ArgumentException>(() => compiler.Compile("<div></div>", "", "{}", scope));

        compiler.LastScope.ShouldBeNull();
    }

    [Test]
    public async Task FakeLayouts_WhenALayoutIsAdded_ShouldAnswerItForItsVersionAndRecordApprovals()
    {
        var layouts = new FakeTemplateLayouts();
        var held = new TemplateLayout(SampleCompiler().Read(CompiledSample()), "0a1b2c3d4e5f60718293a4b5c6d7e8f9", false, [], []);
        var report = new LayoutReport(held.AssetId, ["headline"], []);
        layouts.Add("tpl_fake00000001", 0, held);
        layouts.Outcome(ApproveOutcome.Overflows);

        var draft = await layouts.ForVersionAsync("tpl_fake00000001", 0, CancellationToken.None);
        var published = await layouts.ForVersionAsync("tpl_fake00000001", 1, CancellationToken.None);
        var outcome = await layouts.ApproveAsync("tpl_fake00000001", SAMPLE_NAME, report, CancellationToken.None);

        draft.ShouldBe([held]);
        published.ShouldBeEmpty();
        outcome.ShouldBe(ApproveOutcome.Overflows);
        layouts.ApprovalsFor("tpl_fake00000001").ShouldBe([(SAMPLE_NAME, report)]);
        layouts.ApprovalsFor("tpl_other0000001").ShouldBeEmpty();
    }

    // The definition comes from the catalogue: nothing here checks slides against it.
    private static FakeCustomLayoutCompiler SampleCompiler()
        => new FakeCustomLayoutCompiler().WithDefinition(SAMPLE_NAME, LayoutCatalogue.Find("bullets")!);

    private static string CompiledSample()
        => File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "..", "custom-layout-compiled.sample.json"));
}
