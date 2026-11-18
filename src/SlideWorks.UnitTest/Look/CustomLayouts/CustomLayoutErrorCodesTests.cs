using System.Text.Json;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The error codes of custom layouts (T10): the C# codes are the runtime source, and
// src/contracts/custom-layout-errors.json must say the same.
public class CustomLayoutErrorCodesTests
{
    [Test]
    public void ErrorCodes_WhenCompared_ShouldMatchTheContractFile()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("custom-layout-errors.json")));

        var inFile = file.RootElement.EnumerateObject().Select(entry => (entry.Name, Text: entry.Value.GetString())).ToList();

        inFile.Select(entry => entry.Name).ShouldBe(CustomLayoutErrorCodes.All);
        inFile.ShouldAllBe(entry => entry.Text == CustomLayoutErrorCodes.Text(entry.Name));
        inFile.Count.ShouldBe(44);
    }

    [Test]
    public void ErrorCodes_WhenListed_ShouldEachCarryItsFamilysPrefix()
    {
        var codes = CustomLayoutErrorCodes.All;

        codes.Count(code => code.StartsWith("markup_", StringComparison.Ordinal)).ShouldBe(10);
        codes.Count(code => code.StartsWith("css_", StringComparison.Ordinal)).ShouldBe(13);
        codes.Count(code => code.StartsWith("slots_", StringComparison.Ordinal)).ShouldBe(11);
        codes.ShouldContain(CustomLayoutErrorCodes.BINDING_UNKNOWN_SLOT);
        codes.ShouldContain(CustomLayoutErrorCodes.STEPS_MISMATCH);
        codes.ShouldBeUnique();
    }

    [Test]
    public void Text_WhenTheCodeIsAlsoATemplateError_ShouldBeTheSameSentence()
    {
        var shared = CustomLayoutErrorCodes.All.Intersect(TemplateErrorTexts.Codes).ToList();

        shared.ShouldBe([CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE, CustomLayoutErrorCodes.LAYOUT_REFUSED], ignoreOrder: true);
        shared.ShouldAllBe(code => CustomLayoutErrorCodes.Text(code) == TemplateErrorTexts.Format(code));
    }

    [Test]
    public void Text_WhenTheCodeIsUnknown_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => CustomLayoutErrorCodes.Text("no_such_code")).Message.ShouldContain("no_such_code");
    }

    [Test]
    public void Of_WhenGivenACode_ShouldCarryItsText()
    {
        var error = CustomLayoutError.Of("layout.css:3", CustomLayoutErrorCodes.CSS_IMPORTANT);

        error.ShouldBe(new CustomLayoutError("layout.css:3", "css_important", "!important is not allowed."));
    }

    [TestCase("bars")]
    [TestCase("data-each")]
    [TestCase("flex_grow")]
    [TestCase("h2")]
    public void Of_WhenGivenACheckedIdentifier_ShouldAppendItToTheCodesText(string identifier)
    {
        var error = CustomLayoutError.Of("slots.bars", CustomLayoutErrorCodes.SLOT_UNUSED, identifier);

        error.Code.ShouldBe("slot_unused");
        error.Path.ShouldBe("slots.bars");
        error.Message.ShouldBe($"{CustomLayoutErrorCodes.Text(CustomLayoutErrorCodes.SLOT_UNUSED)} ({identifier})");
    }

    [TestCase("")]
    [TestCase("Bars")]
    [TestCase("two words")]
    [TestCase("<script>")]
    [TestCase("a.b")]
    [TestCase("url(x)")]
    [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Of_WhenTheIdentifierIsNotChecked_ShouldThrow(string identifier)
    {
        Should.Throw<ArgumentException>(() => CustomLayoutError.Of("layout.html:1", CustomLayoutErrorCodes.MARKUP_TAG, identifier)).ParamName.ShouldBe("identifier");
    }
}
