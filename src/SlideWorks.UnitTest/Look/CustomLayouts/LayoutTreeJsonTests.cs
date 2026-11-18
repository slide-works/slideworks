using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The tree's JSON (T10) against src/contracts/custom-layout-tree.sample.json. That sample is the
// author's side: the tree as the markup stage returns it, with no scope and bare class names. The
// compiled tree, with the scope and every class prefixed, is inside custom-layout-compiled.sample.json.
public class LayoutTreeJsonTests
{
    private static string Sample => File.ReadAllText(ContractFiles.PathOf("custom-layout-tree.sample.json"));

    [Test]
    public void Tree_WhenRoundTripped_ShouldBeUnchanged()
    {
        var tree = LayoutTreeJson.Read(Sample);

        var written = LayoutTreeJson.Write(tree);

        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(Sample)).ShouldBeTrue(written);
        LayoutTreeJson.Write(LayoutTreeJson.Read(written)).ShouldBe(written);
    }

    [Test]
    public void Read_WhenTheSampleIsRead_ShouldHoldTheBarChartsBindings()
    {
        var tree = LayoutTreeJson.Read(Sample);

        tree.Version.ShouldBe(1);
        tree.Scope.ShouldBeNull();
        var row = tree.Nodes().Single(node => node.Each is not null);
        row.Each.ShouldBe("bars");
        row.Step.ShouldBe(0);
        var bar = tree.Nodes().Single(node => node.Vars.Count > 0);
        bar.Vars.ShouldBe([new TreeVar("--v", "share")]);
        bar.Variants.ShouldBe([TreeVariant.Bare("state")]);
        tree.Nodes().Single(node => node.If is not null).Slot.ShouldBe("note");
        tree.VarNames().ShouldBe(["--v"]);
    }

    [Test]
    public void Write_WhenAFieldIsUnset_ShouldLeaveItOutAndKeepEveryList()
    {
        var tree = new LayoutTree(1, null, [new TreeNode("div", [], null, null, null, null, [], [], [])]);

        var written = LayoutTreeJson.Write(tree);

        written.ShouldBe("""{"version":1,"root":[{"tag":"div","classes":[],"vars":[],"variants":[],"children":[]}]}""");
    }

    [Test]
    public void Write_WhenTheScopeIsSet_ShouldCarryIt()
    {
        var compiled = JsonNode.Parse(File.ReadAllText(ContractFiles.PathOf("custom-layout-compiled.sample.json")))!["tree"]!.ToJsonString();
        var tree = LayoutTreeJson.Read(compiled);

        var read = LayoutTreeJson.Read(LayoutTreeJson.Write(tree));

        read.Scope.ShouldBe("0a1b2c3d");
        LayoutTreeJson.Write(read).ShouldBe(compiled);
    }

    [Test]
    public void Read_WhenAVariantOfACompiledTreeIsRead_ShouldHoldAClassPerValueInOrder()
    {
        const string json = """{"version":1,"scope":"0a1b2c3d","root":[{"tag":"div","classes":[],"vars":[],"variants":[{"slot":"state","classes":{"normal":"cl-0a1b2c3d-x__is-normal","alert":"cl-0a1b2c3d-x__is-alert"}}],"children":[]}]}""";

        var tree = LayoutTreeJson.Read(json);

        var variant = tree.Root[0].Variants.ShouldHaveSingleItem();
        variant.Slot.ShouldBe("state");
        variant.Classes.ShouldBe([new VariantClass("normal", "cl-0a1b2c3d-x__is-normal"), new VariantClass("alert", "cl-0a1b2c3d-x__is-alert")]);
        variant.ClassOf("alert").ShouldBe("cl-0a1b2c3d-x__is-alert");
        variant.ClassOf("unknown").ShouldBeNull();
        LayoutTreeJson.Write(tree).ShouldBe(json);
    }

    [Test]
    public void Read_WhenAStepIsTheMaximum_ShouldKeepIt()
    {
        var tree = LayoutTreeJson.Read(Tree($$"""{"tag":"div","classes":[],"step":{{CustomLayoutLimits.MAX_STEPS}},"vars":[],"variants":[],"children":[]}"""));

        tree.Root[0].Step.ShouldBe(CustomLayoutLimits.MAX_STEPS);
        Should.Throw<JsonException>(() => LayoutTreeJson.Read(Tree($$"""{"tag":"div","classes":[],"step":{{CustomLayoutLimits.MAX_STEPS + 1}},"vars":[],"variants":[],"children":[]}""")));
    }

    [Test]
    public void Read_WhenTheTreeIsDeeperThanTheLimit_ShouldThrow()
    {
        var atTheLimit = LayoutTreeJson.Read(Tree(Nested(CustomLayoutLimits.MAX_MARKUP_DEPTH)));

        atTheLimit.Nodes().Count().ShouldBe(CustomLayoutLimits.MAX_MARKUP_DEPTH);
        Should.Throw<JsonException>(() => LayoutTreeJson.Read(Tree(Nested(CustomLayoutLimits.MAX_MARKUP_DEPTH + 1))));
    }

    [Test]
    public void Read_WhenTheTreeHasMoreNodesThanTheLimit_ShouldThrow()
    {
        var atTheLimit = LayoutTreeJson.Read(Tree(Siblings(CustomLayoutLimits.MAX_NODES)));

        atTheLimit.Nodes().Count().ShouldBe(CustomLayoutLimits.MAX_NODES);
        Should.Throw<JsonException>(() => LayoutTreeJson.Read(Tree(Siblings(CustomLayoutLimits.MAX_NODES + 1))));
    }

    [Test]
    public void Read_WhenAKeyIsUnknown_ShouldNotQuoteItInTheMessage()
    {
        var thrown = Should.Throw<JsonException>(() => LayoutTreeJson.Read("""{"version":1,"root":[],"onclick-secret":1}"""));

        thrown.Message.ShouldNotContain("onclick-secret");
    }

    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"vars":[],"variants":[],"children":[],"text":"hello"}]}""")]
    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"vars":[],"variants":[]}]}""")]
    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"step":-1,"vars":[],"variants":[],"children":[]}]}""")]
    [TestCase("""{"version":2,"root":[]}""")]
    [TestCase("""{"version":1,"scope":"0A1B2C3D","root":[]}""")]
    [TestCase("""{"version":1,"root":[],"html":"<b>"}""")]
    [TestCase("""{"root":[]}""")]
    [TestCase("""{"version":"1","root":[]}""")]
    [TestCase("""{"version":null,"root":[]}""")]
    [TestCase("""{"version":1.5,"root":[]}""")]
    [TestCase("""{"version":1,"version":1,"root":[]}""")]
    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"step":"2","vars":[],"variants":[],"children":[]}]}""")]
    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"step":null,"vars":[],"variants":[],"children":[]}]}""")]
    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"step":1.5,"vars":[],"variants":[],"children":[]}]}""")]
    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"step":21,"vars":[],"variants":[],"children":[]}]}""")]
    [TestCase("""{"version":1,"root":[{"tag":7,"classes":[],"vars":[],"variants":[],"children":[]}]}""")]
    [TestCase("""{"version":1,"root":["div"]}""")]
    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"vars":[],"variants":[{"slot":"state","classes":{"alert":"cl-0a1b2c3d-x__is-alert"}}],"children":[]}]}""")]
    [TestCase("""{"version":1,"scope":"0a1b2c3d","root":[{"tag":"div","classes":[],"vars":[],"variants":["state"],"children":[]}]}""")]
    [TestCase("""{"version":1,"scope":"0a1b2c3d","root":[{"tag":"div","classes":[],"vars":[],"variants":[{"slot":"state"}],"children":[]}]}""")]
    [TestCase("""{"version":1,"scope":"0a1b2c3d","root":[{"tag":"div","classes":[],"vars":[],"variants":[{"slot":"state","classes":{"alert":7}}],"children":[]}]}""")]
    [TestCase("""{"version":1,"scope":"0a1b2c3d","root":[{"tag":"div","classes":[],"vars":[],"variants":[{"slot":"state","classes":{},"prefix":"cl-"}],"children":[]}]}""")]
    public void Read_WhenTheJsonIsNotTheContracts_ShouldThrow(string json)
    {
        Should.Throw<JsonException>(() => LayoutTreeJson.Read(json));
    }

    private static string Tree(string nodes) => $$"""{"version":1,"root":[{{nodes}}]}""";

    private static string Siblings(int count) =>
        string.Join(",", Enumerable.Repeat("""{"tag":"div","classes":[],"vars":[],"variants":[],"children":[]}""", count));

    private static string Nested(int depth) =>
        Enumerable.Range(0, depth).Aggregate(string.Empty, (inner, _) => $$"""{"tag":"div","classes":[],"vars":[],"variants":[],"children":[{{inner}}]}""");
}
