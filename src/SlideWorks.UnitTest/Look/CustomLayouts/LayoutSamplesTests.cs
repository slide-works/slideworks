using System.Globalization;
using System.Text.Json;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Slides;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The sample and maximum slots a layout's owner reviews it with (T10): they must always pass the layout's own
// definition, and the maximum must really fill every limit.
public class LayoutSamplesTests
{
    private static readonly SlotSchemaCompiler Compiler = new();
    private static readonly LayoutSamples Samples = new();

    private static IReadOnlyList<string> Schemas => [.. FiveSchemas.All, SampleSchema()];

    private static string SampleSchema() =>
        System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ContractFiles.PathOf("custom-layout-upload.sample.json")))!["slots"]!.ToJsonString();

    private static (LayoutDefinition Definition, CustomLayoutSchema Schema) Compile(string json)
    {
        var (definition, schema, errors) = Compiler.Compile(json, 0);
        errors.ShouldBeEmpty();
        return (definition!, schema!);
    }

    private static List<string> Problems(LayoutDefinition definition, string slots)
    {
        using var document = JsonDocument.Parse(slots);
        var validation = new SlotValidation();
        definition.Validate(document.RootElement, validation);
        return [.. validation.Errors];
    }

    private static string Doc(string slots) =>
        $$"""{ "name": "my-layout", "description": "A layout for the tests.", "surface": "light", "slots": { {{slots}} } }""";

    [Test]
    public void Maximum_WhenGenerated_ShouldFillEverySlotToItsLimit()
    {
        var (_, schema) = Compile(FiveSchemas.AGENT_LANES);

        using var document = JsonDocument.Parse(Samples.Maximum(schema));
        var root = document.RootElement;

        root.GetProperty("kicker").GetString()!.Length.ShouldBe(40);
        root.GetProperty("headline").GetString()!.Length.ShouldBe(80);
        root.GetProperty("lead").GetString()!.Length.ShouldBe(160);
        root.GetProperty("lanes").GetArrayLength().ShouldBe(4);
        root.GetProperty("lanes")[3].GetProperty("task").GetString()!.Length.ShouldBe(44);
        root.GetProperty("lanes")[3].GetProperty("progress").GetInt32().ShouldBe(100);
        root.GetProperty("lanes")[0].GetProperty("state").GetString().ShouldBe("running");
        root.TryGetProperty("reveal", out _).ShouldBeFalse();
    }

    [Test]
    public void Maximum_WhenGenerated_ShouldWriteProseOfShortWordsWithWideLettersInEveryThirdWord()
    {
        var (_, schema) = Compile(Doc("\"t\": { \"type\": \"text\", \"max\": 200 }"));

        using var document = JsonDocument.Parse(Samples.Maximum(schema));
        var text = document.RootElement.GetProperty("t").GetString()!;
        var words = text.Split(' ');

        text.Length.ShouldBe(200);
        words.ShouldAllBe(w => w.Length >= 4 && w.Length <= 9);
        text.ShouldNotContain("  ");
        words.Where((_, i) => i % 3 == 2).ShouldAllBe(w => w.Contains('m') || w.Contains('w'));
    }

    [TestCase(FiveSchemas.AGENT_LANES)]
    [TestCase(FiveSchemas.DIAL)]
    [TestCase(FiveSchemas.CODE_MOCK)]
    [TestCase(FiveSchemas.CHAT_MOCK)]
    [TestCase(FiveSchemas.HORIZON)]
    public void Maximum_WhenGenerated_ShouldValidate(string json)
    {
        var (definition, schema) = Compile(json);

        Problems(definition, Samples.Maximum(schema)).ShouldBeEmpty();
    }

    [Test]
    public void Maximum_WhenGeneratedForTheSampleSchema_ShouldValidate()
    {
        var (definition, schema) = Compile(SampleSchema());

        Problems(definition, Samples.Maximum(schema)).ShouldBeEmpty();
    }

    [TestCase(FiveSchemas.AGENT_LANES)]
    [TestCase(FiveSchemas.DIAL)]
    [TestCase(FiveSchemas.CODE_MOCK)]
    [TestCase(FiveSchemas.CHAT_MOCK)]
    [TestCase(FiveSchemas.HORIZON)]
    public void Sample_WhenGenerated_ShouldValidate(string json)
    {
        var (definition, schema) = Compile(json);

        Problems(definition, Samples.Sample(schema)).ShouldBeEmpty();
    }

    [Test]
    public void Sample_WhenGeneratedForTheSampleSchema_ShouldValidate()
    {
        var (definition, schema) = Compile(SampleSchema());

        Problems(definition, Samples.Sample(schema)).ShouldBeEmpty();
    }

    [Test]
    public void Maximum_WhenGeneratedTwice_ShouldBeIdentical()
    {
        foreach (var json in Schemas)
        {
            var (_, schema) = Compile(json);

            Samples.Maximum(schema).ShouldBe(Samples.Maximum(schema));
            Samples.Sample(schema).ShouldBe(Samples.Sample(schema));
        }
    }

    [Test]
    public void Sample_WhenAListHasAnEnum_ShouldShowEveryValue()
    {
        var (_, schema) = Compile(FiveSchemas.HORIZON);

        using var document = JsonDocument.Parse(Samples.Sample(schema));
        var fills = document.RootElement.GetProperty("rows").EnumerateArray()
            .SelectMany(row => row.GetProperty("segments").EnumerateArray())
            .Select(segment => segment.GetProperty("fill").GetString())
            .Distinct()
            .ToList();

        fills.ShouldBe(["solid", "hatched", "faint", "none"], ignoreOrder: true);
    }

    [Test]
    public void Sample_WhenAListHasANumber_ShouldSpreadTheValuesSoBarsDiffer()
    {
        var (_, schema) = Compile(FiveSchemas.AGENT_LANES);

        using var document = JsonDocument.Parse(Samples.Sample(schema));
        var lanes = document.RootElement.GetProperty("lanes");
        var progress = lanes.EnumerateArray().Select(l => l.GetProperty("progress").GetInt32()).ToList();

        lanes.GetArrayLength().ShouldBe(3);
        progress.Distinct().Count().ShouldBe(3);
        progress.ShouldBeInOrder();
        progress.ShouldAllBe(p => p > 0 && p < 100);
    }

    [Test]
    public void Sample_WhenGenerated_ShouldKeepTextsShortAndPresentEveryOptionalSlot()
    {
        var (_, schema) = Compile(FiveSchemas.AGENT_LANES);

        using var document = JsonDocument.Parse(Samples.Sample(schema));
        var root = document.RootElement;

        root.GetProperty("lead").GetString()!.Length.ShouldBe(60);
        root.GetProperty("headline").GetString()!.Length.ShouldBe(40);
        root.GetProperty("kicker").GetString()!.Length.ShouldBe(20);
    }

    [Test]
    public void Sample_WhenAListHasNoMinimum_ShouldHoldOneItemAndNeverMoreThanItsMax()
    {
        var (_, schema) = Compile(Doc("""
            "a": { "type": "list", "min": 0, "max": 5, "of": { "type": "text", "max": 9 } },
            "b": { "type": "list", "min": 3, "max": 3, "of": { "type": "text", "max": 9 } }
            """));

        using var document = JsonDocument.Parse(Samples.Sample(schema));

        document.RootElement.GetProperty("a").GetArrayLength().ShouldBe(1);
        document.RootElement.GetProperty("b").GetArrayLength().ShouldBe(3);
    }

    [Test]
    public void Maximum_WhenASlotIsLevels_ShouldFillTheBlockToItsLimits()
    {
        var (definition, schema) = Compile(FiveSchemas.AGENT_LANES);

        using var document = JsonDocument.Parse(Samples.Maximum(schema));
        var levels = document.RootElement.GetProperty("levels");
        var items = levels.GetProperty("items");

        items.GetArrayLength().ShouldBe(7);
        items[0].GetProperty("text").GetString()!.Length.ShouldBe(50);
        items[0].GetProperty("mark").GetString()!.Length.ShouldBe(3);
        items[0].GetProperty("state").GetString().ShouldBe("current");
        levels.GetProperty("title").GetString()!.Length.ShouldBe(30);
        Problems(definition, Samples.Maximum(schema)).ShouldBeEmpty();
    }

    [Test]
    public void Maximum_WhenSlotsAreEachKindOfBlock_ShouldFillThemToTheirLimits()
    {
        var (definition, schema) = Compile(Doc("""
            "callout": { "type": "callout" }, "chip": { "type": "chip" }, "badge": { "type": "badge" }, "facts": { "type": "facts" }
            """));

        using var document = JsonDocument.Parse(Samples.Maximum(schema));
        var root = document.RootElement;

        root.GetProperty("callout").GetProperty("text").GetString()!.Length.ShouldBe(220);
        root.GetProperty("chip").GetString()!.Length.ShouldBe(24);
        root.GetProperty("badge").GetString()!.Length.ShouldBe(InlineBlocks.BADGE);
        root.GetProperty("facts").GetArrayLength().ShouldBe(4);
        root.GetProperty("facts")[0].GetProperty("value").GetString()!.Length.ShouldBe(28);
        Problems(definition, Samples.Maximum(schema)).ShouldBeEmpty();
        Problems(definition, Samples.Sample(schema)).ShouldBeEmpty();
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Maximum_WhenATextsMaxIsTwo_ShouldBeOneWordOfTwoLetters(int max)
    {
        var (definition, schema) = Compile(Doc($"\"t\": {{ \"type\": \"text\", \"max\": {max} }}"));

        using var document = JsonDocument.Parse(Samples.Maximum(schema));
        var text = document.RootElement.GetProperty("t").GetString()!;

        text.Length.ShouldBe(max);
        text.ShouldAllBe(c => c >= 'a' && c <= 'z');
        Problems(definition, Samples.Sample(schema)).ShouldBeEmpty();
        JsonDocument.Parse(Samples.Sample(schema)).RootElement.GetProperty("t").GetString()!.Length.ShouldBeGreaterThan(0);
    }

    [Test]
    public void Maximum_WhenATextIsEveryLengthFromOneToFiveHundred_ShouldBeExactlyThatLong()
    {
        var wrong = Enumerable.Range(1, 500).Where(length =>
        {
            var (_, schema) = Compile(Doc($"\"t\": {{ \"type\": \"text\", \"max\": {length} }}"));
            using var document = JsonDocument.Parse(Samples.Maximum(schema));
            var words = document.RootElement.GetProperty("t").GetString()!.Split(' ');
            return string.Join(' ', words).Length != length || (length >= 4 && words.Any(w => w.Length is < 4 or > 9));
        }).ToList();

        wrong.ShouldBeEmpty();
    }

    [Test]
    public void Sample_WhenAnImageIsOptional_ShouldLeaveItOut()
    {
        var (definition, schema) = Compile(Doc("\"t\": { \"type\": \"text\", \"max\": 9 }, \"pic\": { \"type\": \"image\", \"optional\": true }"));

        using var sample = JsonDocument.Parse(Samples.Sample(schema));
        using var maximum = JsonDocument.Parse(Samples.Maximum(schema));

        sample.RootElement.TryGetProperty("pic", out _).ShouldBeFalse();
        maximum.RootElement.TryGetProperty("pic", out _).ShouldBeFalse();
        Problems(definition, Samples.Maximum(schema)).ShouldBeEmpty();
    }

    [Test]
    public void Maximum_WhenANumberHasDecimalBounds_ShouldStayInsideThem()
    {
        var (definition, schema) = Compile(Doc("\"n\": { \"type\": \"number\", \"min\": 0.5, \"max\": 1.5 }"));

        Problems(definition, Samples.Maximum(schema)).ShouldBeEmpty();
        Problems(definition, Samples.Sample(schema)).ShouldBeEmpty();
        JsonDocument.Parse(Samples.Maximum(schema)).RootElement.GetProperty("n").GetDouble().ToString(CultureInfo.InvariantCulture).ShouldBe("1.5");
    }

    [Test]
    public void Sample_WhenAnIntegerRangeIsNarrow_ShouldGiveDistinctFractionalValues()
    {
        var (definition, schema) = Compile(Doc("\"rows\": { \"type\": \"list\", \"min\": 3, \"max\": 3, \"of\": { \"type\": \"number\", \"min\": 0, \"max\": 1 } }"));

        Problems(definition, Samples.Sample(schema)).ShouldBeEmpty();
        var values = JsonDocument.Parse(Samples.Sample(schema)).RootElement.GetProperty("rows").EnumerateArray().Select(v => v.GetDouble()).ToList();
        values.Distinct().Count().ShouldBe(3);
        Problems(definition, Samples.Maximum(schema)).ShouldBeEmpty();
    }

    [Test]
    public void Samples_WhenARangeIsInsideOneInteger_ShouldUseTheRealBounds()
    {
        var (definition, schema) = Compile(Doc("\"n\": { \"type\": \"number\", \"min\": 0.1, \"max\": 0.9 }, \"m\": { \"type\": \"number\", \"min\": 0, \"max\": 2.5 }"));

        Problems(definition, Samples.Maximum(schema)).ShouldBeEmpty();
        Problems(definition, Samples.Sample(schema)).ShouldBeEmpty();
        var maximum = JsonDocument.Parse(Samples.Maximum(schema)).RootElement;
        maximum.GetProperty("n").GetDouble().ShouldBe(0.9);
        maximum.GetProperty("m").GetDouble().ShouldBe(2.5);
        var sample = JsonDocument.Parse(Samples.Sample(schema)).RootElement;
        sample.GetProperty("n").GetDouble().ShouldBeInRange(0.1, 0.9);
        sample.GetProperty("m").GetDouble().ShouldBeInRange(0, 2.5);
    }

    [Test]
    public void Maximum_WhenSlotsAreNegativeAndNested_ShouldValidate()
    {
        var (definition, schema) = Compile(Doc("""
            "rows": { "type": "list", "min": 1, "max": 12, "of": {
              "n": { "type": "number", "min": -100, "max": -1 },
              "inner": { "type": "list", "min": 2, "max": 3, "of": { "type": "enum", "values": ["a", "b", "c"] } } } }
            """));

        Problems(definition, Samples.Maximum(schema)).ShouldBeEmpty();
        Problems(definition, Samples.Sample(schema)).ShouldBeEmpty();
    }
}
