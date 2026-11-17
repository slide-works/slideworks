using System.Text.Json.Nodes;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Slides;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The pipeline's order and the stored form of what it returns (T10), with a fake for every stage.
// The fake schema stage hands out the built-in bullets definition beside a bar-chart schema, and the
// fake samples are slots that definition accepts.
public class CustomLayoutPipelineTests
{
    private const string SCOPE = "0a1b2c3d";
    private const string SLOTS_JSON = """{ "name": "bar-chart", "slots": {} }""";
    private const string BULLETS_SLOTS = """{"headline":"Where the week went","items":["Meetings","Building"]}""";

    private static readonly CustomLayoutSchema Schema = new(
        "bar-chart",
        "A headline over bars.",
        "light",
        "bars",
        [new CustomSlot("headline", "text", false, null, 80, [], [], null)]);

    private static LayoutTree SampleTree => LayoutTreeJson.Read(Contract("custom-layout-tree.sample.json"));

    [Test]
    public void Compile_WhenStagesRefuse_ShouldReturnEveryStagesErrors()
    {
        var stages = new Stages
        {
            MarkupErrors = [CustomLayoutError.Of("layout.html:2", CustomLayoutErrorCodes.MARKUP_TAG)],
            SchemaErrors = [CustomLayoutError.Of("slots.bars", CustomLayoutErrorCodes.SLOTS_LIMIT), CustomLayoutError.Of("slots.note", CustomLayoutErrorCodes.SLOTS_TYPE)],
            CssErrors = [CustomLayoutError.Of("layout.css:7", CustomLayoutErrorCodes.CSS_COLOUR)],
        };

        var result = stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE);

        result.Layout.ShouldBeNull();
        result.Errors.Select(error => error.Code).ShouldBe(["markup_tag", "slots_limit", "slots_type", "css_colour"]);
        result.Errors.Select(error => error.Path).ShouldBe(["layout.html:2", "slots.bars", "slots.note", "layout.css:7"]);
        stages.FixedSteps.ShouldBe(0);
        stages.ClassPrefix.ShouldBe("cl-0a1b2c3d-x__");
        stages.BoundVars.ShouldBeEmpty();
    }

    [Test]
    public void Compile_WhenAStageRefuses_ShouldNotRunTheCrossCheck()
    {
        var stages = new Stages { CssErrors = [CustomLayoutError.Of("layout.css:1", CustomLayoutErrorCodes.CSS_IMPORTANT)] };

        var result = stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE);

        result.Layout.ShouldBeNull();
        result.Errors.ShouldBe([CustomLayoutError.Of("layout.css:1", CustomLayoutErrorCodes.CSS_IMPORTANT)]);
        stages.CrossChecks.ShouldBe(0);
        stages.SampleCalls.ShouldBe(0);
    }

    [Test]
    public void Compile_WhenTheCrossCheckRefuses_ShouldReturnItsErrors()
    {
        var stages = new Stages { CrossErrors = [CustomLayoutError.Of("slots.note", CustomLayoutErrorCodes.SLOT_UNUSED)] };

        var result = stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE);

        result.Layout.ShouldBeNull();
        result.Errors.ShouldBe([CustomLayoutError.Of("slots.note", CustomLayoutErrorCodes.SLOT_UNUSED)]);
        stages.CrossChecks.ShouldBe(1);
        stages.SampleCalls.ShouldBe(0);
    }

    [Test]
    public void Compile_WhenAllStagesPass_ShouldReturnTheCompiledLayout()
    {
        var stages = new Stages();

        var result = stages.Pipeline().Compile("<div>", ".bar {}", SLOTS_JSON, SCOPE);

        result.Errors.ShouldBeEmpty();
        var layout = result.Layout.ShouldNotBeNull();
        stages.ClassPrefix.ShouldBe("cl-0a1b2c3d-bar-chart__");
        stages.BoundVars.ShouldBe(["--v"]);
        stages.GivenCss.ShouldBe(".bar {}");
        layout.Tree.Scope.ShouldBe(SCOPE);
        LayoutTreeJson.Write(layout.Tree with { Scope = null }).ShouldBe(LayoutTreeJson.Write(SampleTree));
        layout.CompilerVersion.ShouldBe(1);
        layout.Name.ShouldBe("bar-chart");
        layout.Description.ShouldBe("A headline over bars.");
        layout.Surface.ShouldBe("light");
        layout.Steps.ShouldBe(new CustomSteps(CustomSteps.LIST, "bars", 0));
        layout.Definition.ShouldBeSameAs(LayoutCatalogue.Find("bullets"));
        layout.Css.ShouldBe(".cl-0a1b2c3d-bar-chart__bar {}");
        layout.SlotsJson.ShouldBe(SLOTS_JSON);
        layout.SampleJson.ShouldBe(BULLETS_SLOTS);
        layout.MaximumJson.ShouldBe(BULLETS_SLOTS);
    }

    [Test]
    public void Compile_WhenTheTreeNumbersItsSteps_ShouldGiveTheSchemaStageTheHighest()
    {
        var stepped = new TreeNode("div", [], null, null, null, 3, [], [], []);
        var stages = new Stages { Tree = new LayoutTree(1, null, [stepped with { Step = 1 }, stepped]) };

        var result = stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE);

        result.Errors.ShouldBeEmpty();
        stages.FixedSteps.ShouldBe(3);
    }

    [TestCase("")]
    [TestCase("0a1b2c3")]
    [TestCase("0a1b2c3d4")]
    [TestCase("0A1B2C3D")]
    [TestCase("0a1b2c3g")]
    [TestCase("0a1b-c3d")]
    public void Compile_WhenTheScopeIsNotEightHexCharacters_ShouldThrow(string scope)
    {
        var stages = new Stages();

        Should.Throw<ArgumentException>(() => stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, scope)).ParamName.ShouldBe("scope");
        stages.MarkupCalls.ShouldBe(0);
    }

    [Test]
    public void Compile_WhenASampleFailsTheDefinition_ShouldThrow()
    {
        var stages = new Stages { MaximumSlots = """{"headline":"No items"}""" };

        Should.Throw<InvalidOperationException>(() => stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE)).Message.ShouldContain("maximum");
    }

    [Test]
    public void Compiled_WhenRoundTripped_ShouldBeUnchanged()
    {
        var stages = new Stages();
        var layout = stages.Pipeline().Compile("<div>", ".bar {}", SLOTS_JSON, SCOPE).Layout.ShouldNotBeNull();

        var written = CompiledLayoutJson.Write(layout);
        var read = CompiledLayoutJson.Read(written, stages);

        CompiledLayoutJson.Write(read).ShouldBe(written);
        JsonNode.Parse(written)!["compilerVersion"]!.GetValue<int>().ShouldBe(1);
        read.CompilerVersion.ShouldBe(1);
        read.Steps.ShouldBe(layout.Steps);
        read.Definition.ShouldBeSameAs(layout.Definition);
        read.Tree.Scope.ShouldBe(SCOPE);
        read.SlotsJson.ShouldBe(SLOTS_JSON);
        (read with { Definition = layout.Definition, Tree = layout.Tree }).ShouldBe(layout);
    }

    [Test]
    public void Compiled_WhenTheContractSampleIsRead_ShouldWriteTheSameDocument()
    {
        var sample = Contract("custom-layout-compiled.sample.json");

        var layout = CompiledLayoutJson.Read(sample, new Stages());

        JsonNode.DeepEquals(JsonNode.Parse(CompiledLayoutJson.Write(layout)), JsonNode.Parse(sample)).ShouldBeTrue();
        layout.CompilerVersion.ShouldBe(CompiledLayout.COMPILER_VERSION);
        layout.Name.ShouldBe("bar-chart");
        layout.Steps.ShouldBe(new CustomSteps("list", "bars", 0));
        layout.Tree.Scope.ShouldBe(SCOPE);
        layout.Css.ShouldContain(".cl-0a1b2c3d-bar-chart__bar.cl-0a1b2c3d-bar-chart__is-alert {");
        LayoutTreeJson.Write(layout.Tree with { Scope = null }).ShouldBe(LayoutTreeJson.Write(SampleTree));
    }

    [Test]
    public void Compiled_WhenTheVersionIsMissing_ShouldReadAsZero()
    {
        var old = JsonNode.Parse(Contract("custom-layout-compiled.sample.json"))!.AsObject();
        old.Remove("compilerVersion");

        var layout = CompiledLayoutJson.Read(old.ToJsonString(), new Stages());

        layout.CompilerVersion.ShouldBe(0);
    }

    [Test]
    public void Compiled_WhenTheSchemaNoLongerCompiles_ShouldThrow()
    {
        var refusing = new Stages { SchemaErrors = [CustomLayoutError.Of("slots.bars", CustomLayoutErrorCodes.SLOTS_LIMIT)] };

        Should.Throw<InvalidOperationException>(() => CompiledLayoutJson.Read(Contract("custom-layout-compiled.sample.json"), refusing))
            .Message.ShouldContain("slots_limit");
    }

    [Test]
    public void Upload_WhenTheContractSampleIsRead_ShouldBeTheThreePartsOfTheCompiledSample()
    {
        var upload = JsonNode.Parse(Contract("custom-layout-upload.sample.json"))!.AsObject();
        var compiled = JsonNode.Parse(Contract("custom-layout-compiled.sample.json"))!.AsObject();

        upload.Select(part => part.Key).ShouldBe(["markup", "css", "slots"]);
        upload["markup"]!.GetValue<string>().ShouldNotContain("<style");
        upload["css"]!.GetValue<string>().ShouldContain("width: calc(var(--v) * 1%)");
        JsonNode.DeepEquals(upload["slots"], compiled["schema"]).ShouldBeTrue();
        compiled["css"]!.GetValue<string>().ShouldBe(upload["css"]!.GetValue<string>().Replace(".", ".cl-0a1b2c3d-bar-chart__", StringComparison.Ordinal));
    }

    private static string Contract(string name) => File.ReadAllText(ContractFiles.PathOf(name));

    // Every stage in one fake: each passes unless it is given errors, and records what it was given.
    private sealed class Stages : IMarkupSanitiser, ICssSanitiser, ISlotSchemaCompiler, ILayoutCrossCheck, ILayoutSamples
    {
        public LayoutTree Tree { get; init; } = SampleTree;

        public IReadOnlyList<CustomLayoutError> MarkupErrors { get; init; } = [];

        public IReadOnlyList<CustomLayoutError> SchemaErrors { get; init; } = [];

        public IReadOnlyList<CustomLayoutError> CssErrors { get; init; } = [];

        public IReadOnlyList<CustomLayoutError> CrossErrors { get; init; } = [];

        public string MaximumSlots { get; init; } = BULLETS_SLOTS;

        public int MarkupCalls { get; private set; }

        public int CrossChecks { get; private set; }

        public int SampleCalls { get; private set; }

        public int FixedSteps { get; private set; } = -1;

        public string? ClassPrefix { get; private set; }

        public string? GivenCss { get; private set; }

        public IReadOnlyList<string> BoundVars { get; private set; } = [];

        public CustomLayoutPipeline Pipeline() => new(this, this, this, this, this);

        public (LayoutTree? Tree, IReadOnlyList<CustomLayoutError> Errors) Sanitise(string markup)
        {
            MarkupCalls++;
            return (MarkupErrors.Count == 0 ? Tree : null, MarkupErrors);
        }

        public (string? Css, IReadOnlyList<CustomLayoutError> Errors) Sanitise(string css, string classPrefix, IReadOnlyList<string> boundVars)
        {
            GivenCss = css;
            ClassPrefix = classPrefix;
            BoundVars = boundVars;
            return (CssErrors.Count == 0 ? css.Replace(".", "." + classPrefix, StringComparison.Ordinal) : null, CssErrors);
        }

        public (LayoutDefinition? Definition, CustomLayoutSchema? Schema, IReadOnlyList<CustomLayoutError> Errors) Compile(string slotsJson, int fixedSteps)
        {
            FixedSteps = fixedSteps;
            return SchemaErrors.Count == 0 ? (LayoutCatalogue.Find("bullets"), Schema, SchemaErrors) : (null, null, SchemaErrors);
        }

        public (CustomSteps? Steps, IReadOnlyList<CustomLayoutError> Errors) Check(LayoutTree tree, CustomLayoutSchema schema)
        {
            CrossChecks++;
            return (CrossErrors.Count == 0 ? new CustomSteps(CustomSteps.LIST, schema.Steps, 0) : null, CrossErrors);
        }

        public string Sample(CustomLayoutSchema schema)
        {
            SampleCalls++;
            return BULLETS_SLOTS;
        }

        public string Maximum(CustomLayoutSchema schema)
        {
            SampleCalls++;
            return MaximumSlots;
        }
    }
}
