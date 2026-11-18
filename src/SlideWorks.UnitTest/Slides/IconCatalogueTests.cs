using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class IconCatalogueTests
{
    [Test]
    public void Names_WhenListed_ShouldMatchTheContractFile()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("icons.json")));
        var names = file.RootElement.GetProperty("icons").EnumerateArray().Select(icon => icon.GetProperty("name").GetString()!).ToList();

        IconCatalogue.Names.ShouldBe(names);
        IconCatalogue.Names.Count.ShouldBe(48);
        IconCatalogue.Names.ShouldBeUnique();
    }

    [Test]
    public void Validate_WhenTheNameIsInTheSet_ShouldAccept()
    {
        Errors("\"coins\"").ShouldBeEmpty();
        IconSlot.BLOCK_HEIGHT.ShouldBe(78);
    }

    [Test]
    public void Validate_WhenTheNameIsUnknown_ShouldRefuseWithTheNearNames()
    {
        Errors("\"coin\"").ShouldBe(["cards.cards[0].icon: unknown icon 'coin'; did you mean 'coins', 'code'?"]);
        Errors("\"check\"").ShouldBeEmpty();
        Errors("\"chec\"").ShouldBe(["cards.cards[0].icon: unknown icon 'chec'; did you mean 'check', 'check-circle', 'chat'?"]);
    }

    [Test]
    public void Validate_WhenManyNamesAreNear_ShouldListAtMostFive()
    {
        IconSlot.NearNames("co").Count.ShouldBeLessThanOrEqualTo(5);
        IconSlot.NearNames("l").Count.ShouldBeLessThanOrEqualTo(5);
        IconSlot.NearNames("book").ShouldBe(["book", "bolt", "lock"]);
    }

    [TestCase("\"zzzzzzzzzz\"", "cards.cards[0].icon: unknown icon 'zzzzzzzzzz'")]
    [TestCase("\"Coins \"", "cards.cards[0].icon: unknown icon 'Coins '; did you mean 'coins'?")]
    [TestCase("\"\"", "cards.cards[0].icon: unknown icon ''")]
    [TestCase("7", "cards.cards[0].icon: expected the name of an icon")]
    public void Validate_WhenNothingIsNear_ShouldRefuseWithoutAHint(string value, string error)
    {
        Errors(value).ShouldBe([error]);
    }

    [Test]
    public void Validate_WhenTheNameIsVeryLong_ShouldEchoItCut()
    {
        var errors = Errors("\"" + new string('a', 5000) + "\"");

        errors.Count.ShouldBe(1);
        errors[0].Length.ShouldBeLessThan(120);
    }

    [Test]
    public void ToJsonSchema_WhenAsked_ShouldListEveryName()
    {
        var schema = new IconSlot().ToJsonSchema();

        schema["type"]!.GetValue<string>().ShouldBe("string");
        schema["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(IconCatalogue.Names);
    }

    private static IReadOnlyList<string> Errors(string json)
    {
        using var document = JsonDocument.Parse(json);
        var validation = new SlotValidation();
        new IconSlot().Validate(document.RootElement, "cards.cards[0].icon", validation);
        return validation.Errors;
    }
}
