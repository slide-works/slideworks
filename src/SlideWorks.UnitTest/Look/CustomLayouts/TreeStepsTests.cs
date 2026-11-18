using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

public class TreeStepsTests
{
    [Test]
    public void FixedCount_WhenStepsAreNumbered_ShouldBeTheHighest()
    {
        var tree = new LayoutTree(1, null, [Node(1, Node(null, Node(3)), Node(2)), Node(null)]);

        TreeSteps.FixedCount(tree).ShouldBe(3);
    }

    [Test]
    public void FixedCount_WhenNoStepIsNumbered_ShouldBeZero()
    {
        var listSteps = LayoutTreeJson.Read(File.ReadAllText(ContractFiles.PathOf("custom-layout-tree.sample.json")));

        TreeSteps.FixedCount(listSteps).ShouldBe(0);
        TreeSteps.FixedCount(new LayoutTree(1, null, [Node(null)])).ShouldBe(0);
        TreeSteps.FixedCount(new LayoutTree(1, null, [])).ShouldBe(0);
    }

    private static TreeNode Node(int? step, params TreeNode[] children) => new("div", [], null, null, null, step, [], [], children);
}
