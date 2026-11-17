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
        bar.Variants.ShouldBe(["state"]);
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
        var tree = LayoutTreeJson.Read(Sample) with { Scope = "0a1b2c3d" };

        var read = LayoutTreeJson.Read(LayoutTreeJson.Write(tree));

        read.Scope.ShouldBe("0a1b2c3d");
    }

    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"vars":[],"variants":[],"children":[],"text":"hello"}]}""")]
    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"vars":[],"variants":[]}]}""")]
    [TestCase("""{"version":1,"root":[{"tag":"div","classes":[],"step":-1,"vars":[],"variants":[],"children":[]}]}""")]
    [TestCase("""{"version":2,"root":[]}""")]
    [TestCase("""{"version":1,"scope":"0A1B2C3D","root":[]}""")]
    [TestCase("""{"version":1,"root":[],"html":"<b>"}""")]
    public void Read_WhenTheJsonIsNotTheContracts_ShouldThrow(string json)
    {
        Should.Throw<JsonException>(() => LayoutTreeJson.Read(json));
    }
}
