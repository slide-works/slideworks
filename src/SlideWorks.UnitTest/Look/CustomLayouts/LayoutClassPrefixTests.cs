using SlideWorks.Core.Look.CustomLayouts;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The one function that makes a custom layout's class prefix and puts it on a class.
public class LayoutClassPrefixTests
{
    [Test]
    public void Prefix_WhenTheScopeAndNameAreValid_ShouldBeTheScopedPrefix()
    {
        var prefix = LayoutClassPrefix.Of("0a1b2c3d", "bar-chart");

        prefix.Value.ShouldBe("cl-0a1b2c3d-bar-chart__");
        prefix.ToString().ShouldBe(prefix.Value);
        prefix.Apply("bar").ShouldBe("cl-0a1b2c3d-bar-chart__bar");
        prefix.Apply("is-alert").ShouldBe("cl-0a1b2c3d-bar-chart__is-alert");
        LayoutClassPrefix.Of("0a1b2c3d", LayoutScope.UNNAMED).Value.ShouldBe("cl-0a1b2c3d-x__");
    }

    [TestCase("")]
    [TestCase("0a1b2c3")]
    [TestCase("0a1b2c3d4")]
    [TestCase("0A1B2C3D")]
    [TestCase("0a1b2c3g")]
    [TestCase("0a1b-c3d")]
    [TestCase(" a1b2c3d")]
    public void Prefix_WhenTheScopeIsNotEightHex_ShouldThrow(string scope)
    {
        Should.Throw<ArgumentException>(() => LayoutClassPrefix.Of(scope, "bar-chart")).ParamName.ShouldBe("scope");
    }

    [TestCase("")]
    [TestCase("Bar-Chart")]
    [TestCase("1bar")]
    [TestCase("-bar")]
    [TestCase("bar-")]
    [TestCase("bar--chart")]
    [TestCase("bar_chart")]
    [TestCase("bar__chart")]
    [TestCase("bar chart")]
    [TestCase("bar.chart")]
    [TestCase("bar{}")]
    public void Prefix_WhenTheLayoutNameIsNotAName_ShouldThrow(string layoutName)
    {
        Should.Throw<ArgumentException>(() => LayoutClassPrefix.Of("0a1b2c3d", layoutName)).ParamName.ShouldBe("layoutName");
    }

    [Test]
    public void Apply_WhenTheClassAlreadyStartsWithCl_ShouldStillPrefixIt()
    {
        var prefix = LayoutClassPrefix.Of("0a1b2c3d", "bar-chart");

        prefix.Apply("cl-ffffffff-other").ShouldBe("cl-0a1b2c3d-bar-chart__cl-ffffffff-other");
        prefix.Apply("cl-0a1b2c3d-bar-chart").ShouldBe("cl-0a1b2c3d-bar-chart__cl-0a1b2c3d-bar-chart");
    }

    [TestCase("")]
    [TestCase("a b")]
    [TestCase("Bar")]
    [TestCase("a__b")]
    [TestCase("a.b")]
    [TestCase("a\tb")]
    public void Apply_WhenTheClassIsNotAClassName_ShouldThrow(string className)
    {
        Should.Throw<ArgumentException>(() => LayoutClassPrefix.Of("0a1b2c3d", "bar-chart").Apply(className)).ParamName.ShouldBe("className");
    }
}
