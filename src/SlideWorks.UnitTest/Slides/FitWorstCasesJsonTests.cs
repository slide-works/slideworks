using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// src/contracts/fit-worst-cases.json is generated here (SW-694): the worst-text twins of every accepted fit
// fixture, each run through the server's own SlideValidator, so the client's FitWorstCase.spec.tsx draws only
// the ones the server accepts and never guesses legality. Each fixture gives its case as it stands, then one
// case per region and variant with that region's drawn text swapped for a twin of the same length while the
// other regions stay at the fixture:
// - allW: unbroken W keeping line breaks, the widest text a font draws and the worst case for
//   overflow-wrap:anywhere;
// - capitals: prose in capitals, line by line (maxText.capitals in the client).
// A same-length twin is not always legal: the fit estimate wraps word by word, so a twin that wraps
// differently can need more lines. An emphasis phrase stays in its text (only the text around it is
// swapped), and the emphasis slot itself is a reference to drawn text, not text, so it gets no twin.
[TestFixture]
public class FitWorstCasesJsonTests
{
    private const string FILE = "fit-worst-cases.json";

    private const string REGENERATE =
        "UPDATE_LAYOUTS_DOC=1 tools/agent/test-dotnet src/SlideWorks.UnitTest \"FullyQualifiedName~FitWorstCasesJsonTests\"";

    private const string FIT_ERROR = "too much text to fit";
    private const string LENGTH_ERROR = " characters (got ";

    // Slot fields that hold a value the layout reads, not text it draws (an enum, an id, a url, an icon
    // name, alt text, an emphasis reference). A twin leaves them as they are.
    private static readonly HashSet<string> _notText =
    [
        "alt", "count", "emphasis", "fill", "frame", "icon", "image", "in", "kind", "position", "reveal", "side",
        "state", "status", "tone", "url",
    ];

    // The texts an emphasis phrase without 'in' may sit in: every target the layouts pass to EmphasisRules.Check.
    private static readonly HashSet<string> _emphasisTargets =
        ["headline", "caption", "failure.line", "success.line", "quote", "statement", "sub"];

    private static readonly (string Name, Func<string, string> Twin)[] _variants =
    [
        ("allW", text => new string(text.Select(c => c == '\n' ? '\n' : 'W').ToArray())),
        ("capitals", text => string.Join('\n', text.Split('\n').Select(line => Capitals(line.Length)))),
    ];

    private static readonly JsonSerializerOptions _compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Test]
    public void Json_WhenFixturesOrRulesChange_ShouldMatchTheGeneratedFile()
    {
        var expected = Render();
        var path = ContractFiles.PathOf(FILE);
        if (Environment.GetEnvironmentVariable("UPDATE_LAYOUTS_DOC") == "1")
        {
            File.WriteAllText(path, expected);
        }

        var actual = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;

        (actual == expected).ShouldBeTrue($"src/contracts/{FILE} is missing or out of date: regenerate it with `{REGENERATE}` and commit it");
    }

    [Test]
    public void Cases_WhenGenerated_ShouldIncludeEveryAcceptedFixtureAsItStandsAndLegal()
    {
        var cases = Cases().ToList();

        cases.Where(c => c["region"]!.GetValue<string>() == "fixture" && !c["legal"]!.GetValue<bool>())
            .Select(c => $"{c["fixture"]}: {c["reason"]}").ShouldBeEmpty();
        cases.Count(c => c["region"]!.GetValue<string>() == "fixture").ShouldBe(AcceptedFixtures().Count());
    }

    private static string Render()
    {
        var lines = Cases().Select(c => "    " + c.ToJsonString(_compact));
        return "{\n"
            + $"  \"generated\": {JsonSerializer.Serialize($"From the fit fixtures and SlideValidator; do not edit by hand. Regenerate: {REGENERATE}", _compact)},\n"
            + "  \"cases\": [\n"
            + string.Join(",\n", lines) + "\n"
            + "  ]\n"
            + "}\n";
    }

    private static IEnumerable<(string Name, JsonObject Fixture)> AcceptedFixtures() =>
        ContractFiles.FilesIn("fit-fixtures")
            .SelectMany(path => JsonNode.Parse(File.ReadAllText(path))!.AsObject().Select(f => (f.Key, f.Value!.AsObject())))
            .Where(f => f.Item2["accepted"]?.GetValue<bool>() ?? true);

    private static IEnumerable<JsonObject> Cases()
    {
        foreach (var (name, fixture) in AcceptedFixtures())
        {
            var slots = fixture["slots"]!.AsObject();
            yield return Case(name, fixture, "fixture", null, slots.DeepClone());
            foreach (var (region, value) in slots)
            {
                foreach (var (variant, twin) in _variants)
                {
                    var swapped = TwinOf(value, region, region, slots, twin);
                    if (JsonNode.DeepEquals(swapped, value))
                    {
                        continue;
                    }

                    var caseSlots = slots.DeepClone().AsObject();
                    caseSlots[region] = swapped;
                    yield return Case(name, fixture, region, variant, caseSlots);
                }
            }
        }
    }

    private static JsonObject Case(string name, JsonObject fixture, string region, string? variant, JsonNode slots)
    {
        var result = Validate(fixture, slots);
        var reason = string.Join("; ", result.Errors);
        var item = new JsonObject
        {
            ["fixture"] = name,
            ["layout"] = fixture["layout"]!.GetValue<string>(),
            ["region"] = region,
            ["variant"] = variant,
            ["legal"] = result.IsValid,
        };
        if (fixture["surface"] is not null)
        {
            item["surface"] = fixture["surface"]!.GetValue<string>();
        }

        if (!result.IsValid)
        {
            item["refusedBy"] = RefusedBy(reason);
            item["reason"] = reason;
        }

        item["slots"] = slots;
        return item;
    }

    private static string RefusedBy(string reason)
    {
        if (reason.Contains(FIT_ERROR, StringComparison.Ordinal))
        {
            return "fit";
        }

        if (reason.Contains(LENGTH_ERROR, StringComparison.Ordinal))
        {
            return "length";
        }

        return reason.Contains(".emphasis", StringComparison.Ordinal) ? "emphasis" : "other";
    }

    // `value` with every drawn string swapped by `twin`, keeping any emphasis phrase that sits in it. `path`
    // is the dotted path of object keys from the slots (array indices are left out, as emphasis targets are).
    private static JsonNode? TwinOf(JsonNode? value, string key, string path, JsonObject slots, Func<string, string> twin)
    {
        if (_notText.Contains(key) || value is null)
        {
            return value?.DeepClone();
        }

        return value switch
        {
            JsonArray array => new JsonArray(array.Select(item => TwinOf(item, key, path, slots, twin)).ToArray()),
            JsonObject obj => new JsonObject(obj.Select(p =>
                KeyValuePair.Create(p.Key, TwinOf(p.Value, p.Key, $"{path}.{p.Key}", slots, twin)))),
            JsonValue text when text.GetValueKind() == JsonValueKind.String =>
                KeepingPhrases(text.GetValue<string>(), PhrasesIn(path, slots), twin),
            _ => value.DeepClone(),
        };
    }

    // The emphasis phrases that name the text at `path`: by 'in', or, without it, an emphasis target.
    private static List<string> PhrasesIn(string path, JsonObject slots) =>
        slots["emphasis"] is JsonArray entries
            ? entries.OfType<JsonObject>()
                .Where(e => e["in"] is JsonValue target ? target.GetValue<string>() == path : _emphasisTargets.Contains(path))
                .Select(e => e["text"]?.GetValue<string>())
                .OfType<string>()
                .ToList()
            : [];

    private static string KeepingPhrases(string text, List<string> phrases, Func<string, string> twin)
    {
        var spans = phrases
            .Select(p => (Start: text.IndexOf(p, StringComparison.Ordinal), p.Length))
            .Where(s => s.Start >= 0)
            .OrderBy(s => s.Start)
            .ToList();
        var result = new System.Text.StringBuilder();
        var at = 0;
        foreach (var (start, length) in spans)
        {
            if (start < at)
            {
                continue;
            }

            result.Append(twin(text[at..start])).Append(text, start, length);
            at = start + length;
        }

        return result.Append(twin(text[at..])).ToString();
    }

    // maxText.ts proseFill(length).toUpperCase(): ordinary words, wide letters first, cut to `length`.
    private static string Capitals(int length)
    {
        string[] words = ["Maximum", "Width", "words", "wrap", "across", "the", "whole", "slide"];
        var text = "";
        var i = 0;
        while (text.Length < length)
        {
            text += (text.Length > 0 ? " " : "") + words[i++ % words.Length];
        }

        return text[..length].TrimEnd().PadRight(length, 'm').ToUpperInvariant();
    }

    private static SlideValidationResult Validate(JsonObject fixture, JsonNode slots)
    {
        using var document = JsonDocument.Parse(slots.ToJsonString());
        var surface = fixture["surface"]?.GetValue<string>();
        return surface is null
            ? SlideValidator.Validate(fixture["layout"]!.GetValue<string>(), document.RootElement)
            : SlideValidator.Validate(LayoutCatalogue.Find(fixture["layout"]!.GetValue<string>())!, document.RootElement, FitWidths.Default, surface);
    }
}
