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

    private const string PREFIX = "cl-0a1b2c3d-bar-chart__";

    // The author's side: the tree as the markup stage returns it, with no scope and bare class names.
    private static LayoutTree MarkupStageTree => LayoutTreeJson.Read(Contract("custom-layout-tree.sample.json"));

    // The compiled side: the tree inside the stored layout, with the scope and every class prefixed.
    private static LayoutTree CompiledSampleTree =>
        LayoutTreeJson.Read(JsonNode.Parse(Contract("custom-layout-compiled.sample.json"))!["tree"]!.ToJsonString());

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
        LayoutTreeJson.Write(layout.Tree).ShouldBe(LayoutTreeJson.Write(CompiledSampleTree));
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
    public void Compile_WhenAllStagesPass_ShouldPrefixEveryTreeClass()
    {
        var stages = new Stages();

        var layout = stages.Pipeline().Compile("<div>", ".bar {}", SLOTS_JSON, SCOPE).Layout.ShouldNotBeNull();

        var given = MarkupStageTree.Nodes().SelectMany(node => node.Classes).ToList();
        var compiled = layout.Tree.Nodes().SelectMany(node => node.Classes).ToList();
        given.ShouldNotBeEmpty();
        compiled.ShouldBe(given.Select(name => PREFIX + name));
        compiled.ShouldAllBe(name => name.StartsWith(stages.ClassPrefix!, StringComparison.Ordinal));
        compiled.Intersect(given).ShouldBeEmpty();
        layout.Tree.Nodes().Count().ShouldBe(MarkupStageTree.Nodes().Count());
    }

    [Test]
    public void Compile_WhenAnAuthorsClassAlreadyStartsWithCl_ShouldStillPrefixIt()
    {
        var child = new TreeNode("span", ["cl-0a1b2c3d-bar-chart--bar", "cl-ffffffff-other"], null, null, null, null, [], [], []);
        var root = new TreeNode("div", ["cl-0a1b2c3d-bar-chart"], null, null, null, null, [], [], [child]);
        var stages = new Stages { Tree = new LayoutTree(1, null, [root]) };

        var layout = stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE).Layout.ShouldNotBeNull();

        layout.Tree.Nodes().SelectMany(node => node.Classes).ShouldBe(
        [
            "cl-0a1b2c3d-bar-chart__cl-0a1b2c3d-bar-chart",
            "cl-0a1b2c3d-bar-chart__cl-0a1b2c3d-bar-chart--bar",
            "cl-0a1b2c3d-bar-chart__cl-ffffffff-other",
        ]);
    }

    [TestCase("bar chart")]
    [TestCase("")]
    [TestCase("Bar")]
    [TestCase("bar__x")]
    public void Compile_WhenTheMarkupStageLetsABadClassThrough_ShouldThrow(string className)
    {
        var stages = new Stages { Tree = new LayoutTree(1, null, [new TreeNode("div", [className], null, null, null, null, [], [], [])]) };

        Should.Throw<ArgumentException>(() => stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE)).ParamName.ShouldBe("className");
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
        layout.Tree.Nodes().SelectMany(node => node.Classes).ShouldAllBe(name => name.StartsWith(PREFIX, StringComparison.Ordinal));
    }

    [Test]
    public void Samples_WhenTheCompiledTreeLosesItsPrefix_ShouldBeTheMarkupStagesTree()
    {
        var compiled = LayoutTreeJson.Write(CompiledSampleTree with { Scope = null });

        compiled.Replace(PREFIX, string.Empty, StringComparison.Ordinal).ShouldBe(LayoutTreeJson.Write(MarkupStageTree));
        MarkupStageTree.Scope.ShouldBeNull();
        MarkupStageTree.Nodes().SelectMany(node => node.Classes).ShouldAllBe(name => !name.StartsWith("cl-", StringComparison.Ordinal));
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
        public LayoutTree Tree { get; init; } = MarkupStageTree;

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
