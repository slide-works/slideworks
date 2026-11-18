using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class LayoutCatalogueTests
{
    // The JSON Schema 2020-12 keywords the catalogue may emit; anything else is a typo.
    private static readonly HashSet<string> _keywords =
    [
        "$schema", "title", "description", "type", "properties", "required", "additionalProperties", "items",
        "minItems", "maxItems", "minLength", "maxLength", "pattern", "minimum", "maximum", "enum", "oneOf", "not",
    ];

    private static readonly HashSet<string> _types = ["string", "number", "integer", "object", "array", "boolean", "null"];

    [Test]
    public void Layouts_WhenCollected_ShouldMatchTheNamesFile()
    {
        using var names = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("layout-names.json")));

        LayoutCatalogue.LayoutNames.ShouldBe(names.RootElement.GetProperty("layouts").EnumerateArray().Select(n => n.GetString()!));
        LayoutCatalogue.Layouts.Where(l => l.Dark).Select(l => l.Name).ShouldBe(["title", "divider", "close", "join"]);
    }

    [Test]
    public void Layouts_WhenSurfaceTrackerAndSteps_ShouldSayThemOnEveryDefinition()
    {
        LayoutCatalogue.Layouts.Where(l => l.Surface == LayoutSurface.Either).Select(l => l.Name).ShouldBe(["bullets", "big-number", "statement"]);
        LayoutCatalogue.Layouts.Where(l => !l.Tracker).Select(l => l.Name).ShouldBe(["statement"]);
        LayoutCatalogue.Layouts.Where(l => l.Steps is not null).Select(l => (l.Name, l.StepsFrom))
            .ShouldBe([("bullets", "items"), ("two-column", null), ("big-number", null), ("image", "points"), ("table", "rows"), ("numbered-cards", "cards"), ("rules", "rules"), ("cards", "cards")]);
    }

    [TestCase("statement", "kicker,statement")]
    [TestCase("links", "kicker,headline")]
    [TestCase("stack", "kicker,headline")]
    [TestCase("bars", "kicker,headline")]
    [TestCase("flow", "kicker,headline")]
    [TestCase("levels", "kicker,headline")]
    public void Find_WhenLayoutIsAStub_ShouldHaveItsTwoTextSlots(string name, string fields)
    {
        var stub = LayoutCatalogue.Find(name)!;

        stub.Slots.Fields.Select(f => f.Name).ShouldBe(fields.Split(','));
        stub.Description.ShouldNotBeNullOrWhiteSpace();
        stub.Slots.Fields[1].Required.ShouldBeTrue();
        stub.Slots.Fields[1].Type.ShouldBeOfType<TextSlot>().MaxLength.ShouldBe(name == "statement" ? 120 : 80);
    }

    [Test]
    public void Layouts_WhenCollected_ShouldKeepTheFrozenOrder()
    {
        LayoutDefinition[] definitions =
        [
            TitleLayout.Definition, DividerLayout.Definition, BulletsLayout.Definition, TwoColumnLayout.Definition,
            PracticeLayout.Definition, CompareLayout.Definition, BigNumberLayout.Definition, QuoteLayout.Definition,
            ImageLayout.Definition, TableLayout.Definition, ChartLayout.Definition, GanttLayout.Definition,
            NumberedCardsLayout.Definition, RulesLayout.Definition, CardsLayout.Definition, StatementLayout.Definition,
            LinksLayout.Definition, StackLayout.Definition, BarsLayout.Definition, FlowLayout.Definition,
            LevelsLayout.Definition, CloseLayout.Definition, JoinLayout.Definition, PollLayout.Definition,
            QaLayout.Definition,
        ];

        LayoutCatalogue.Layouts.Count.ShouldBe(definitions.Length);
        for (var i = 0; i < definitions.Length; i++)
        {
            LayoutCatalogue.Layouts[i].ShouldBeSameAs(definitions[i]);
        }

        definitions.Select(d => d.Name).ShouldBe(
        [
            "title", "divider", "bullets", "two-column", "practice", "compare", "big-number", "quote", "image",
            "table", "chart", "gantt", "numbered-cards", "rules", "cards", "statement", "links", "stack", "bars",
            "flow", "levels", "close", "join", "poll", "qa",
        ]);
    }

    [Test]
    public void ToJsonSchema_WhenEveryLayout_ShouldBeValidJsonSchemaObject()
    {
        foreach (var layout in LayoutCatalogue.Layouts)
        {
            var schema = JsonNode.Parse(layout.ToJsonSchema().ToJsonString())!.AsObject();

            schema["$schema"]!.GetValue<string>().ShouldBe("https://json-schema.org/draft/2020-12/schema");
            schema["title"]!.GetValue<string>().ShouldBe(layout.Name);
            schema["description"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
            schema["type"]!.GetValue<string>().ShouldBe("object");
            schema["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
            var problems = new List<string>();
            CheckSchema(schema, layout.Name, problems);
            problems.ShouldBeEmpty();
        }
    }

    [Test]
    public void ToJsonSchema_WhenEveryBlock_ShouldBeValidJsonSchemaObject()
    {
        foreach (var block in LayoutCatalogue.Blocks)
        {
            var schema = JsonNode.Parse(block.ToJsonSchema().ToJsonString())!.AsObject();

            schema["title"]!.GetValue<string>().ShouldBe(block.Name);
            var problems = new List<string>();
            CheckSchema(schema, block.Name, problems);
            problems.ShouldBeEmpty();
        }
    }

    [Test]
    public void Find_WhenLayoutIsQa_ShouldHaveOneOptionalTitleSlot()
    {
        var qa = LayoutCatalogue.Find("qa")!;

        var title = qa.Slots.Fields.ShouldHaveSingleItem();
        title.Name.ShouldBe("title");
        title.Required.ShouldBeFalse();
        title.Type.ShouldBeOfType<TextSlot>().MaxLength.ShouldBe(80);
        qa.Dark.ShouldBeFalse();
        qa.StepsFrom.ShouldBeNull();
    }

    [Test]
    public void ToJsonSchema_WhenPractice_ShouldRequireDoWithLeadAndText()
    {
        var schema = LayoutCatalogue.Find("practice")!.ToJsonSchema();

        var doItems = schema["properties"]!["do"]!;
        doItems["type"]!.GetValue<string>().ShouldBe("array");
        doItems["maxItems"]!.GetValue<int>().ShouldBe(5);
        doItems["items"]!["required"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["lead", "text"]);
    }

    // A oneOf branch such as { "required": ["items"] } names the properties of the schema around it.
    private static void CheckSchema(JsonNode? node, string path, List<string> problems, JsonNode? outerProperties = null)
    {
        if (node is not JsonObject schema)
        {
            problems.Add($"{path}: not an object");
            return;
        }

        foreach (var (key, value) in schema)
        {
            CheckKeyword(key, value, schema["properties"] ?? outerProperties, $"{path}/{key}", problems);
        }

        if (schema["minItems"] is { } min && schema["maxItems"] is { } max && min.GetValue<int>() > max.GetValue<int>())
        {
            problems.Add($"{path}: minItems > maxItems");
        }
    }

    private static void CheckKeyword(string key, JsonNode? value, JsonNode? properties, string path, List<string> problems)
    {
        switch (key)
        {
            case var k when !_keywords.Contains(k):
                problems.Add($"{path}: unknown keyword");
                break;
            case "type" when value is not JsonValue || !_types.Contains(value.GetValue<string>()):
                problems.Add($"{path}: not a JSON Schema type");
                break;
            case "properties":
                foreach (var (name, property) in value!.AsObject())
                {
                    CheckSchema(property, $"{path}/{name}", problems);
                }

                break;
            case "required":
                foreach (var name in value!.AsArray().Select(n => n!.GetValue<string>()).Where(n => properties?[n] is null))
                {
                    problems.Add($"{path}: '{name}' is not a property");
                }

                break;
            case "items" or "not":
                CheckSchema(value, path, problems);
                break;
            case "oneOf":
                foreach (var branch in value!.AsArray())
                {
                    CheckSchema(branch, path, problems, properties);
                }

                break;
            case "enum" when value!.AsArray().Count == 0:
                problems.Add($"{path}: empty enum");
                break;
            case "pattern":
                _ = new Regex(value!.GetValue<string>(), RegexOptions.None, TimeSpan.FromSeconds(1));
                break;
            case "minItems" or "maxItems" or "minLength" or "maxLength" when value!.GetValue<int>() < 0:
                problems.Add($"{path}: negative");
                break;
            default:
                break;
        }
    }
}
