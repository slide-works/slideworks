using System.Text.Json;
using SlideWorks.Core.Slides.Layouts;
using SlideWorks.Core.Translations;

namespace SlideWorks.UnitTest.Translations;

// SlideTranslation.Rekey (D13): entries follow their text to a new path, stay on a path that is still a line, and are
// removed otherwise.
[TestFixture]
public class RekeyTests
{
    [Test]
    public void Rekey_WhenFromMatchesNewPath_ShouldMove()
    {
        var result = Rekey(
            """{ "headline": "Plan", "items": ["Ship daily", "Ship weekly"] }""",
            new() { ["/items/0"] = new("Publica cada semana", "Ship weekly"), ["/headline"] = new("Plan", "Plan") });

        result.Entries.Keys.ShouldBe(["/headline", "/items/1"]);
        result.Entries["/items/1"].ShouldBe(new TranslationEntry("Publica cada semana", "Ship weekly"));
        result.Stale.ShouldBeEmpty();
        result.Removed.ShouldBeEmpty();
    }

    [Test]
    public void Rekey_WhenItemsSwapped_ShouldSwapEntries()
    {
        var result = Rekey(
            """{ "headline": "Plan", "items": ["B", "A"] }""",
            new() { ["/items/0"] = new("a", "A"), ["/items/1"] = new("b", "B") });

        result.Entries["/items/0"].ShouldBe(new TranslationEntry("b", "B"));
        result.Entries["/items/1"].ShouldBe(new TranslationEntry("a", "A"));
        result.Stale.ShouldBeEmpty();
        result.Removed.ShouldBeEmpty();
    }

    [Test]
    public void Rekey_WhenTextChangedAtSamePath_ShouldKeepItStale()
    {
        var result = Rekey(
            """{ "headline": "New plan", "items": ["A"] }""",
            new() { ["/headline"] = new("Plan antiguo", "Old plan") });

        result.Entries["/headline"].ShouldBe(new TranslationEntry("Plan antiguo", "Old plan"));
        result.Stale.ShouldBe(["/headline"]);
        result.Removed.ShouldBeEmpty();
    }

    [Test]
    public void Rekey_WhenPathNoLongerALine_ShouldRemove()
    {
        var result = Rekey(
            """{ "headline": "Plan", "items": ["A"] }""",
            new() { ["/items/1"] = new("b", "B"), ["/nope"] = new("x", "X") });

        result.Entries.ShouldBeEmpty();
        result.Stale.ShouldBeEmpty();
        result.Removed.ShouldBe(["/items/1", "/nope"]);
    }

    [Test]
    public void Rekey_WhenMovedEntryTakesStalePath_ShouldRemoveTheStaleOne()
    {
        var result = Rekey(
            """{ "headline": "Plan", "items": ["New", "A"] }""",
            new() { ["/items/0"] = new("a", "A"), ["/items/1"] = new("b", "B") });

        result.Entries.Keys.ShouldBe(["/items/1"]);
        result.Entries["/items/1"].ShouldBe(new TranslationEntry("a", "A"));
        result.Removed.ShouldBe(["/items/1"]);
    }

    private static RekeyResult Rekey(string slots, Dictionary<string, TranslationEntry> entries)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideTranslation.Rekey(BulletsLayout.Definition, document.RootElement, entries);
    }
}
