using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class InlineBlocksTests
{
    [Test]
    public void Blocks_ShouldBeTheFiveFrozenNames()
    {
        LayoutCatalogue.Blocks.Select(b => b.Name).ShouldBe(["callout", "chip", "badge", "stamp", "journey"]);
        LayoutCatalogue.Blocks.Where(b => !b.SetByAgent).Select(b => b.Name).ShouldBe(["journey"]);
    }
}
