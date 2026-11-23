using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides;

// The fit report, the optional slots, and the room a typical list is given.
[TestFixture]
public class FitReportTests
{
    [Test]
    public void Validate_WhenPracticeIsCompactAndFull_ShouldFit()
    {
        const string slots = """
            {
              "headline": "A full practice",
              "density": "compact",
              "do": [
                { "text": "Slice one change" },
                { "text": "Show it this week" },
                { "text": "Ask what happened" },
                { "text": "Keep the next one small" },
                { "text": "Write the decision down" }
              ],
              "avoid": [ "A big batch", "A layer first", "A quarter of silence", "A slide of caveats" ],
              "comes_back": [ "A customer who saw it", "A decision on Monday" ],
              "callout": { "tone": "accent", "text": "If it cannot be shown, it is not done." }
            }
            """;

        Validate(PracticeLayout.Definition, slots).IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validate_WhenTheSamePracticeIsRegular_ShouldRefuseTheSharedColumn()
    {
        const string slots = """
            {
              "headline": "A full practice",
              "do": [
                { "text": "Slice one change" },
                { "text": "Show it this week" },
                { "text": "Ask what happened" },
                { "text": "Keep the next one small" },
                { "text": "Write the decision down" }
              ],
              "avoid": [ "A big batch", "A layer first", "A quarter of silence", "A slide of caveats" ],
              "comes_back": [ "A customer who saw it", "A decision on Monday" ],
              "callout": { "tone": "accent", "text": "If it cannot be shown, it is not done." }
            }
            """;

        var result = Validate(PracticeLayout.Definition, slots);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldContain("px");
        result.Error.ShouldContain("comes_back and avoid and callout share this region");
    }

    [Test]
    public void Validate_WhenDoHasNoLeadAndComesBackIsOmitted_ShouldAccept()
    {
        const string slots = """{ "headline": "Ship smaller", "do": [{ "text": "Slice the work thinner." }], "avoid": [] }""";

        Validate(PracticeLayout.Definition, slots).IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validate_WhenACardHasNoChips_ShouldAccept()
    {
        const string slots = """{ "headline": "Why", "cards": [{ "title": "One", "text": "First." }] }""";

        Validate(CardsLayout.Definition, slots).IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validate_WhenTheFirstHeaderIsEmpty_ShouldAccept()
    {
        const string slots = """{ "headline": "Was and now", "columns": ["", "Now"], "rows": [["Big", "Small"]] }""";

        Validate(TableLayout.Definition, slots).IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validate_WhenALaterHeaderIsEmpty_ShouldRefuseThatHeader()
    {
        const string slots = """{ "headline": "Was and now", "columns": ["Was", ""], "rows": [["Big", "Small"]] }""";

        Validate(TableLayout.Definition, slots).Error.ShouldContain("columns[1]: must not be empty");
    }

    [Test]
    public void Validate_WhenTwoFieldsAreTooLong_ShouldNameBoth()
    {
        var slots = new JsonObject
        {
            ["headline"] = new string('H', 101),
            ["cards"] = new JsonArray(new JsonObject
            {
                ["title"] = new string('T', 71),
                ["text"] = "Short.",
            }),
        };

        var result = Validate(CardsLayout.Definition, slots.ToJsonString());

        result.Errors.Count.ShouldBeGreaterThan(1);
        result.Error.ShouldContain("headline:");
        result.Error.ShouldContain("title:");
        result.Error.ShouldContain("; ");
    }

    [Test]
    public void Capacity_WhenPractice_ShouldSayHowManyDoItemsFit()
    {
        var capacity = LayoutCapacity.Of(PracticeLayout.Definition).Single(slot => slot.Slot == "do");

        capacity.Fits.ShouldBe(4);
        capacity.ItemPx.ShouldBeGreaterThan(0);
        capacity.BudgetPx.ShouldBe(620);
    }

    [Test]
    public void Capacity_WhenCards_ShouldReportTheRecordedHeightAndGridBudget()
    {
        var capacity = LayoutCapacity.Of(CardsLayout.Definition).Single(slot => slot.Slot == "cards");

        capacity.Note.ShouldBe("4 items of about 74 characters (53 px each, budget 296 px).");
    }

    [Test]
    public void ToJsonSchema_WhenAFieldIsRequired_ShouldListIt()
    {
        var missing = new List<string>();
        foreach (var layout in LayoutCatalogue.Layouts)
        {
            CheckRequired(layout.Slots, layout.ToJsonSchema(), layout.Name, missing);
        }

        missing.ShouldBeEmpty();
    }

    private static void CheckRequired(ObjectSlot slot, JsonObject schema, string path, List<string> missing)
    {
        var listed = schema["required"]?.AsArray().Select(name => name!.GetValue<string>()).ToHashSet() ?? [];
        foreach (var field in slot.Fields.Where(field => field.Required))
        {
            if (!listed.Contains(field.Name))
            {
                missing.Add($"{path}.{field.Name}");
            }
        }

        var properties = schema["properties"]?.AsObject();
        foreach (var field in slot.Fields)
        {
            var child = properties?[field.Name]?.AsObject();
            if (child is null)
            {
                continue;
            }

            switch (field.Type)
            {
                case ObjectSlot nested:
                    CheckRequired(nested, child, $"{path}.{field.Name}", missing);
                    break;
                case ListSlot { Item: ObjectSlot item } when child["items"] is JsonObject items:
                    CheckRequired(item, items, $"{path}.{field.Name}[]", missing);
                    break;
            }
        }
    }

    private static SlideValidationResult Validate(LayoutDefinition definition, string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(definition, document.RootElement, FitWidths.Default);
    }
}
