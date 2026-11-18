using System.Text.Json;
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
        [
            new CustomSlot("headline", "text", false, null, 80, [], [], null),
            new CustomSlot(
                "bars",
                CustomSlot.LIST,
                false,
                2,
                5,
                [],
                [
                    new CustomSlot("label", "text", false, null, 20, [], [], null),
                    new CustomSlot("share", "number", false, 0, 100, [], [], null),
                    new CustomSlot("state", CustomSlot.ENUM, false, null, null, ["normal", "alert"], [], null),
                ],
                null),
            new CustomSlot("note", "text", true, null, 120, [], [], null),
        ]);

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
    public void Compile_WhenAVariantIsBound_ShouldWriteAClassPerEnumValue()
    {
        var stages = new Stages();

        var layout = stages.Pipeline().Compile("<div>", ".bar {}", SLOTS_JSON, SCOPE).Layout.ShouldNotBeNull();

        MarkupStageTree.Nodes().SelectMany(node => node.Variants).ShouldBe([TreeVariant.Bare("state")]);
        var variant = layout.Tree.Nodes().SelectMany(node => node.Variants).ShouldHaveSingleItem();
        variant.Slot.ShouldBe("state");
        variant.Classes.ShouldBe(
        [
            new VariantClass("normal", "cl-0a1b2c3d-bar-chart__is-normal"),
            new VariantClass("alert", "cl-0a1b2c3d-bar-chart__is-alert"),
        ]);
        var prefix = LayoutClassPrefix.Of(SCOPE, "bar-chart");
        variant.Classes!.Select(entry => entry.Class).ShouldBe([prefix.Apply("is-normal"), prefix.Apply("is-alert")]);
        Contract("custom-layout-compiled.sample.json").ShouldContain(".cl-0a1b2c3d-bar-chart__is-alert {");
    }

    [Test]
    public void Compile_WhenAVariantIsBoundToATopLevelEnum_ShouldReadTheTopLevelSlot()
    {
        var schema = Schema with { Slots = [new CustomSlot("tone", CustomSlot.ENUM, false, null, null, ["calm", "loud"], [], null)] };
        var root = new TreeNode("div", ["box"], null, null, null, null, [], [TreeVariant.Bare("tone")], []);
        var stages = new Stages { Tree = new LayoutTree(1, null, [root]), GivenSchema = schema };

        var layout = stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE).Layout.ShouldNotBeNull();

        layout.Tree.Root[0].Variants[0].Classes.ShouldBe(
        [
            new VariantClass("calm", "cl-0a1b2c3d-bar-chart__is-calm"),
            new VariantClass("loud", "cl-0a1b2c3d-bar-chart__is-loud"),
        ]);
    }

    // Inside a data-each a name reads the innermost item only: state is a field of bars, not a top-level slot.
    [TestCase("state", null)]
    [TestCase("headline", null)]
    [TestCase("missing", null)]
    [TestCase("state", "headline")]
    public void Compile_WhenTheCrossCheckLetsABadVariantThrough_ShouldThrow(string slot, string? each)
    {
        var root = new TreeNode("div", [], null, each, null, null, [], [TreeVariant.Bare(slot)], []);
        var stages = new Stages { Tree = new LayoutTree(1, null, [root]) };

        Should.Throw<InvalidOperationException>(() => stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE)).Message.ShouldContain("cross-check");
    }

    // Was Compile_WhenAnEnumValueIsNotAClassName_ShouldThrow ("Very Loud"): the author's slots file
    // must never make Compile throw, so the same input is now an error for the author.
    [TestCaseSource(nameof(EnumValuesThatAreNotClassNames))]
    public void Compile_WhenABoundEnumValueIsNotAClassName_ShouldReturnAnErrorNotThrow(string value)
    {
        var schema = Schema with { Slots = [new CustomSlot("tone", CustomSlot.ENUM, false, null, null, ["calm", value], [], null)] };
        var root = new TreeNode("div", [], null, null, null, null, [], [TreeVariant.Bare("tone")], [new TreeNode("span", [], null, null, null, null, [], [TreeVariant.Bare("tone")], [])]);
        var stages = new Stages { Tree = new LayoutTree(1, null, [root]), GivenSchema = schema };

        var result = stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE);

        result.Layout.ShouldBeNull();
        result.Errors.ShouldBe([CustomLayoutError.Of("slots.tone", CustomLayoutErrorCodes.BINDING_WRONG_KIND, "tone")]);
        result.Errors[0].Message.ShouldBe("This binding needs a slot of another type. (tone)");
        stages.CrossChecks.ShouldBe(1);
        stages.SampleCalls.ShouldBe(0);
    }

    [Test]
    public void Compile_WhenABoundEnumValueIsAsLongAsAllowed_ShouldCompile()
    {
        var longest = new string('a', CustomLayoutLimits.MAX_ENUM_VALUE_CHARS);
        var schema = Schema with { Slots = [new CustomSlot("tone", CustomSlot.ENUM, false, null, null, [longest, "a-1"], [], null)] };
        var root = new TreeNode("div", [], null, null, null, null, [], [TreeVariant.Bare("tone")], []);
        var stages = new Stages { Tree = new LayoutTree(1, null, [root]), GivenSchema = schema };

        var layout = stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE).Layout.ShouldNotBeNull();

        layout.Tree.Root[0].Variants[0].Classes!.Select(entry => entry.Class).ShouldBe([PREFIX + "is-" + longest, PREFIX + "is-a-1"]);
    }

    [Test]
    public void Compile_WhenABoundEnumInsideAListIsNotAClassName_ShouldGiveThePathThroughTheList()
    {
        var bars = Schema.Slots[1];
        var state = bars.Fields[2] with { Values = ["normal", "Alert"] };
        var stages = new Stages { GivenSchema = Schema with { Slots = [Schema.Slots[0], bars with { Fields = [bars.Fields[0], bars.Fields[1], state] }, Schema.Slots[2]] } };

        var result = stages.Pipeline().Compile("<div>", ".bar {}", SLOTS_JSON, SCOPE);

        result.Layout.ShouldBeNull();
        result.Errors.ShouldBe([CustomLayoutError.Of("slots.bars.state", CustomLayoutErrorCodes.BINDING_WRONG_KIND, "state")]);
    }

    [Test]
    public void Compile_WhenAnUnboundEnumValueIsNotAClassName_ShouldCompile()
    {
        var unbound = new CustomSlot("mood", CustomSlot.ENUM, false, null, null, ["Has_Underscore", "UPPER", "two words", new string('a', CustomLayoutLimits.MAX_ENUM_VALUE_CHARS + 1)], [], null);
        var stages = new Stages { GivenSchema = Schema with { Slots = [.. Schema.Slots, unbound] } };

        var result = stages.Pipeline().Compile("<div>", ".bar {}", SLOTS_JSON, SCOPE);

        result.Errors.ShouldBeEmpty();
        result.Layout.ShouldNotBeNull().Tree.Nodes().SelectMany(node => node.Variants).ShouldHaveSingleItem().Slot.ShouldBe("state");
    }

    // What the pipeline compiles, the stored form reads back: with a variant binding, through Write and Read.
    [Test]
    public void Compile_WhenRoundTrippedThroughTheStoredForm_ShouldReadBack()
    {
        var stages = new Stages();
        var layout = stages.Pipeline().Compile("<div>", ".bar {}", SLOTS_JSON, SCOPE).Layout.ShouldNotBeNull();
        layout.Tree.Nodes().SelectMany(node => node.Variants).ShouldHaveSingleItem().Classes.ShouldNotBeNull().Count.ShouldBe(2);

        var stored = CompiledLayoutJson.Write(layout);
        var read = CompiledLayoutJson.Read(stored, stages);

        CompiledLayoutJson.Write(read).ShouldBe(stored);
        LayoutTreeJson.Write(read.Tree).ShouldBe(LayoutTreeJson.Write(layout.Tree));
        (read with { Tree = layout.Tree }).ShouldBe(layout);
    }

    [Test]
    public void Compile_WhenAStepIsOverTheMaximum_ShouldRefuse()
    {
        var stepped = new TreeNode("div", [], null, null, null, CustomLayoutLimits.MAX_STEPS, [], [], []);
        var atTheLimit = new Stages { Tree = new LayoutTree(1, null, [stepped]) };
        var over = new Stages { Tree = new LayoutTree(1, null, [stepped with { Step = CustomLayoutLimits.MAX_STEPS + 1 }]) };

        atTheLimit.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE).Errors.ShouldBeEmpty();
        var result = over.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE);

        result.Layout.ShouldBeNull();
        result.Errors.ShouldBe([CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.STEPS_MISMATCH)]);
        over.CrossChecks.ShouldBe(0);
    }

    [Test]
    public void Compile_WhenAStageReturnsAResultWithAnError_ShouldReturnNoLayout()
    {
        var stages = new Stages { MarkupErrors = [CustomLayoutError.Of("layout.html:3", CustomLayoutErrorCodes.MARKUP_CLASS)], KeepsTheTreeBesideErrors = true };

        var result = stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE);

        result.Layout.ShouldBeNull();
        result.Errors.ShouldBe([CustomLayoutError.Of("layout.html:3", CustomLayoutErrorCodes.MARKUP_CLASS)]);
        stages.BoundVars.ShouldBe(["--v"]);
        stages.CrossChecks.ShouldBe(0);
        stages.SampleCalls.ShouldBe(0);
    }

    [Test]
    public void Compile_WhenAStageReturnsNeitherAResultNorAnError_ShouldThrow()
    {
        var stages = new Stages { ReturnsNoTree = true };

        Should.Throw<InvalidOperationException>(() => stages.Pipeline().Compile("<div>", ".a {}", SLOTS_JSON, SCOPE)).Message.ShouldContain("neither");
        stages.CrossChecks.ShouldBe(0);
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
        read.Tree.Nodes().SelectMany(node => node.Variants).ShouldHaveSingleItem().Classes.ShouldBe(
        [
            new VariantClass("normal", "cl-0a1b2c3d-bar-chart__is-normal"),
            new VariantClass("alert", "cl-0a1b2c3d-bar-chart__is-alert"),
        ]);
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
        var sample = CompiledSampleTree;
        var compiled = LayoutTreeJson.Write(sample with { Scope = null, Root = [.. sample.Root.Select(WithBareVariants)] });

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

    [TestCase("""{"normal":"cl-0a1b2c3d-bar-chart__is-normal","loud":"cl-0a1b2c3d-bar-chart__is-loud"}""")]
    [TestCase("""{"normal":"cl-0a1b2c3d-bar-chart__is-normal"}""")]
    [TestCase("""{"normal":"cl-0a1b2c3d-bar-chart__is-normal","alert":"cl-0a1b2c3d-bar-chart__is-alert","extra":"cl-0a1b2c3d-bar-chart__is-extra"}""")]
    [TestCase("""{"alert":"cl-0a1b2c3d-bar-chart__is-alert","alert":"cl-0a1b2c3d-bar-chart__is-normal"}""")]
    [TestCase("{}")]
    public void Read_WhenAVariantMapKeyIsNotAnEnumValue_ShouldRefuse(string classes)
    {
        var stored = Stored(document => Bar(document)["variants"]![0]!["classes"] = JsonNode.Parse(classes));

        var refused = Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(stored, new Stages()));

        refused.LayoutName.ShouldBe("bar-chart");
        refused.Message.ShouldContain("each value of its enum");
        refused.Message.ShouldNotContain("loud");
        refused.Message.ShouldNotContain("extra");
    }

    [TestCase("state", "note")]
    [TestCase("label", null)]
    [TestCase("missing", null)]
    public void Read_WhenAStoredVariantNamesNoEnumWhereItStands_ShouldRefuse(string slot, string? movedTo)
    {
        var stored = Stored(document =>
        {
            var variants = Bar(document)["variants"]!.AsArray();
            variants[0]!["slot"] = slot;
            if (movedTo is not null)
            {
                var variant = variants[0]!;
                variants.RemoveAt(0);
                document["tree"]!["root"]![0]!["children"]![2]!["variants"]!.AsArray().Add(variant);
            }
        });

        Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(stored, new Stages())).Message.ShouldContain("no enum slot");
    }

    [TestCase("bar")]
    [TestCase("")]
    [TestCase("app-shell")]
    [TestCase("cl-0a1b2c3d-bar-chart_bar")]
    [TestCase("cl-0a1b2c3d-bar-chart__")]
    [TestCase("cl-0a1b2c3d-bar-chart__bar fixed")]
    [TestCase("cl-0a1b2c3d-bar-chart__Bar")]
    [TestCase(" cl-0a1b2c3d-bar-chart__bar")]
    public void Read_WhenAStoredClassLacksThePrefix_ShouldRefuse(string className)
    {
        var inTheTree = Stored(document => Bar(document)["classes"]![0] = className);
        var inAVariant = Stored(document => Bar(document)["variants"]![0]!["classes"]!["alert"] = className);

        var tree = Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(inTheTree, new Stages()));
        var variant = Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(inAVariant, new Stages()));

        tree.LayoutName.ShouldBe("bar-chart");
        tree.Message.ShouldBe("The stored layout 'bar-chart' was refused: a class in its tree does not start with the layout's own prefix.");
        variant.Message.ShouldBe("The stored layout 'bar-chart' was refused: a variant's class does not start with the layout's own prefix.");
    }

    [TestCase("cl-ffffffff-bar-chart__bar")]
    [TestCase("cl-0a1b2c3d-pie-chart__bar")]
    [TestCase("cl-0a1b2c3d-bar__bar")]
    [TestCase("cl-0a1b2c3d-x__bar")]
    public void Read_WhenAStoredClassHasAnotherLayoutsPrefix_ShouldRefuse(string className)
    {
        var inTheTree = Stored(document => Bar(document)["classes"]![0] = className);
        var inAVariant = Stored(document => Bar(document)["variants"]![0]!["classes"]!["alert"] = className);

        var tree = Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(inTheTree, new Stages()));
        var variant = Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(inAVariant, new Stages()));

        tree.LayoutName.ShouldBe("bar-chart");
        tree.Message.ShouldNotContain(className);
        variant.Message.ShouldNotContain(className);
    }

    [Test]
    public void Read_WhenTheTreesScopeIsAnotherUploads_ShouldRefuse()
    {
        var stored = Stored(document => document["tree"]!["scope"] = "ffffffff");
        var unscoped = Stored(document => document["tree"]!.AsObject().Remove("scope"));

        Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(stored, new Stages())).Message.ShouldContain("prefix");
        Should.Throw<JsonException>(() => CompiledLayoutJson.Read(unscoped, new Stages()));
    }

    [Test]
    public void Read_WhenAStoredTreeHasNoVariantsAndNoScope_ShouldRefuse()
    {
        var stored = Stored(document =>
        {
            document["tree"]!.AsObject().Remove("scope");
            Bar(document)["variants"] = new JsonArray();
        });

        Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(stored, new Stages())).Message.ShouldContain("no scope");
    }

    // The same name stored and in the schema, and a stored name that differs from the schema's.
    [TestCase("bar_chart<script>")]
    [TestCase("Bar")]
    [TestCase("evil <b>stored</b> schema name")]
    [TestCase("bar--chart")]
    [TestCase("")]
    public void Read_WhenTheStoredNameIsNotALayoutName_ShouldRefuseWithoutEchoingIt(string name)
    {
        var compiler = new Stages { GivenSchema = Schema with { Name = name } };
        var sameName = Stored(document => document["name"] = name);
        var otherName = Stored(_ => { });

        var same = Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(sameName, compiler));
        var other = Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(otherName, compiler));

        same.Message.ShouldBe("The stored layout 'unnamed' was refused: its name is not a layout name.");
        other.Message.ShouldBe(same.Message);
        same.LayoutName.ShouldBe(StoredLayoutRefusedException.UNNAMED);
    }

    [TestCase("")]
    [TestCase("0a1b2c3")]
    [TestCase("0a1b2c3d4")]
    [TestCase("0A1B2C3D")]
    [TestCase("0a1b2c3g")]
    [TestCase("<b>0a1b2c3d</b>")]
    public void Read_WhenTheStoredScopeIsNotEightHex_ShouldRefuse(string scope)
    {
        var stored = Stored(document => document["tree"]!["scope"] = scope);

        var refused = Should.Throw<JsonException>(() => CompiledLayoutJson.Read(stored, new Stages()));

        refused.Message.ShouldBe("A layout tree's scope is 8 lower-case hex characters.");
    }

    [Test]
    public void Read_WhenAnElementHasTooManyClasses_ShouldRefuse()
    {
        JsonArray Owned(int count) => new([.. Enumerable.Range(0, count).Select(index => (JsonNode)(PREFIX + "c" + index))]);
        var atTheLimit = Stored(document => Bar(document)["classes"] = Owned(CustomLayoutLimits.MAX_CLASSES_PER_ELEMENT));
        var over = Stored(document => Bar(document)["classes"] = Owned(CustomLayoutLimits.MAX_CLASSES_PER_ELEMENT + 1));

        CompiledLayoutJson.Read(atTheLimit, new Stages()).Tree.Nodes().Max(node => node.Classes.Count).ShouldBe(CustomLayoutLimits.MAX_CLASSES_PER_ELEMENT);
        Should.Throw<JsonException>(() => CompiledLayoutJson.Read(over, new Stages())).Message.ShouldBe("An element has more classes than a layout may give one.");
    }

    [TestCase("name", "pie-chart")]
    [TestCase("surface", "dark")]
    public void Read_WhenTheNameOrSurfaceIsNotTheSchemas_ShouldRefuse(string key, string value)
    {
        var stored = Stored(document => document[key] = value);

        var refused = Should.Throw<StoredLayoutRefusedException>(() => CompiledLayoutJson.Read(stored, new Stages()));

        refused.LayoutName.ShouldBe("bar-chart");
        refused.Message.ShouldNotContain(value);
    }

    [Test]
    public void Read_WhenTheDocumentHasAnUnknownMember_ShouldThrow()
    {
        var stored = Stored(document => document["html"] = "<b>");
        var inSteps = Stored(document => document["steps"]!["each"] = true);

        Should.Throw<JsonException>(() => CompiledLayoutJson.Read(stored, new Stages())).Message.ShouldNotContain("html");
        Should.Throw<JsonException>(() => CompiledLayoutJson.Read(inSteps, new Stages()));
    }

    [TestCase("name")]
    [TestCase("description")]
    [TestCase("surface")]
    [TestCase("steps")]
    [TestCase("tree")]
    [TestCase("css")]
    [TestCase("schema")]
    [TestCase("sample")]
    [TestCase("maximum")]
    public void Read_WhenAMemberIsMissing_ShouldThrow(string key)
    {
        var stored = Stored(document => document.Remove(key));

        Should.Throw<JsonException>(() => CompiledLayoutJson.Read(stored, new Stages()));
    }

    [TestCase("""{"kind":"sometimes","count":0}""")]
    [TestCase("""{"kind":"list","count":0}""")]
    [TestCase("""{"kind":"list","list":"bars","count":2}""")]
    [TestCase("""{"kind":"none","list":"bars","count":0}""")]
    [TestCase("""{"kind":"fixed","count":0}""")]
    [TestCase("""{"kind":"fixed","count":21}""")]
    [TestCase("""{"kind":"fixed","count":"3"}""")]
    [TestCase("""{"kind":"none"}""")]
    [TestCase("""{"kind":7,"count":0}""")]
    [TestCase("""["none"]""")]
    public void Read_WhenTheStepsAreNotOneOfTheThreeKinds_ShouldThrow(string steps)
    {
        var stored = Stored(document => document["steps"] = JsonNode.Parse(steps));

        Should.Throw<JsonException>(() => CompiledLayoutJson.Read(stored, new Stages()));
    }

    [TestCase("\"1\"")]
    [TestCase("1.5")]
    [TestCase("null")]
    public void Read_WhenTheCompilerVersionIsNotAWholeNumber_ShouldThrow(string version)
    {
        var stored = Stored(document => document["compilerVersion"] = JsonNode.Parse(version));

        Should.Throw<JsonException>(() => CompiledLayoutJson.Read(stored, new Stages()));
    }

    [Test]
    public void Read_WhenTheStoredTreeIsOverTheLimits_ShouldThrow()
    {
        var leaf = """{"tag":"div","classes":[],"vars":[],"variants":[],"children":[]}""";
        var tooMany = Stored(document => document["tree"]!["root"] = JsonNode.Parse("[" + string.Join(",", Enumerable.Repeat(leaf, CustomLayoutLimits.MAX_NODES + 1)) + "]"));
        var tooDeep = Stored(document => document["tree"]!["root"] = JsonNode.Parse("[" + Enumerable.Range(0, CustomLayoutLimits.MAX_MARKUP_DEPTH + 1).Aggregate(string.Empty, (inner, _) => leaf.Replace("\"children\":[]", "\"children\":[" + inner + "]", StringComparison.Ordinal)) + "]"));

        Should.Throw<JsonException>(() => CompiledLayoutJson.Read(tooMany, new Stages()));
        Should.Throw<JsonException>(() => CompiledLayoutJson.Read(tooDeep, new Stages()));
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

    private static IEnumerable<string> EnumValuesThatAreNotClassNames() =>
        ["Has_Underscore", "UPPER", "two words", "Very Loud", "9lives", "-dash", new string('a', CustomLayoutLimits.MAX_ENUM_VALUE_CHARS + 1), string.Empty];

    private static string Contract(string name) => File.ReadAllText(ContractFiles.PathOf(name));

    // The compiled contract sample with one thing changed.
    private static string Stored(Action<JsonObject> change)
    {
        var document = JsonNode.Parse(Contract("custom-layout-compiled.sample.json"))!.AsObject();
        change(document);
        return document.ToJsonString();
    }

    // The bar of the stored sample: the one element with a variable and a variant.
    private static JsonNode Bar(JsonObject document) =>
        document["tree"]!["root"]![0]!["children"]![1]!["children"]![0]!["children"]![1]!["children"]![0]!;

    private static TreeNode WithBareVariants(TreeNode node) =>
        node with
        {
            Variants = [.. node.Variants.Select(variant => TreeVariant.Bare(variant.Slot))],
            Children = [.. node.Children.Select(WithBareVariants)],
        };

    // Every stage in one fake: each passes unless it is given errors, and records what it was given.
    private sealed class Stages : IMarkupSanitiser, ICssSanitiser, ISlotSchemaCompiler, ILayoutCrossCheck, ILayoutSamples
    {
        public LayoutTree Tree { get; init; } = MarkupStageTree;

        public CustomLayoutSchema GivenSchema { get; init; } = Schema;

        // A markup stage that breaks its contract: a tree beside errors, or neither.
        public bool KeepsTheTreeBesideErrors { get; init; }

        public bool ReturnsNoTree { get; init; }

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
            return (ReturnsNoTree || (MarkupErrors.Count > 0 && !KeepsTheTreeBesideErrors) ? null : Tree, MarkupErrors);
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
            return SchemaErrors.Count == 0 ? (LayoutCatalogue.Find("bullets"), GivenSchema, SchemaErrors) : (null, null, SchemaErrors);
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
