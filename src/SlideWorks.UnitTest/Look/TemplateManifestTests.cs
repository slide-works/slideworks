using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SlideWorks.Core.Look;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

// The template manifest (T3): src/contracts/template-manifest.schema.json, its Northwind sample and ManifestJson.
public class TemplateManifestTests
{
    private static string Sample() => File.ReadAllText(ContractFiles.PathOf("template-manifest.sample.json"));

    private static JsonObject SampleNode() => JsonNode.Parse(Sample())!.AsObject();

    private static IReadOnlyList<ManifestError> ErrorsOf(JsonObject manifest) => ManifestJson.Parse(manifest.ToJsonString()).Errors;

    [Test]
    public void Manifest_WhenRoundTripped_ShouldBeUnchanged()
    {
        var parsed = ManifestJson.Parse(Sample());
        parsed.Errors.ShouldBeEmpty();

        var written = ManifestJson.Write(parsed.Manifest!);

        written.ShouldBe(Sample());
        ManifestJson.Write(ManifestJson.Parse(written).Manifest!).ShouldBe(written);
    }

    [Test]
    public void Manifest_WhenOptionalFieldsAreLeftOut_ShouldRoundTripWithoutThem()
    {
        var manifest = SampleNode();
        manifest.Remove("palette");
        manifest["logos"]!.AsObject().Remove("light");
        manifest["titleArt"] = new JsonObject { ["kind"] = "none" };

        var parsed = ManifestJson.Parse(manifest.ToJsonString());

        parsed.Errors.ShouldBeEmpty();
        parsed.Manifest!.Palette.ShouldBeNull();
        JsonNode.DeepEquals(JsonNode.Parse(ManifestJson.Write(parsed.Manifest)), manifest).ShouldBeTrue();
    }

    [Test]
    public void Manifest_WhenAFieldIsUnknown_ShouldBeRefused()
    {
        var atRoot = SampleNode();
        atRoot["rights"] = true;
        var nested = SampleNode();
        nested["fonts"]![1]!["mono"] = "x";
        var inPalette = SampleNode();
        inPalette["palette"]!["accent-3"] = "#000000";

        ErrorsOf(atRoot).ShouldBe([new ManifestError("rights", "is not a field of a template manifest")]);
        ErrorsOf(nested).ShouldBe([new ManifestError("fonts[1].mono", "is not a field of a template manifest")]);
        ErrorsOf(inPalette).ShouldBe([new ManifestError("palette.accent-3", "is not a field of a template manifest")]);
        ManifestJson.Parse(atRoot.ToJsonString()).Manifest.ShouldBeNull();
    }

    [TestCase("0 1px 2px url(https://example.com/x.png)")]
    [TestCase("0 1px 2px rgb(15 23 42 / 0.06), 0 0 0 url(x)")]
    [TestCase("0 1px 2px var(--sw-accent)")]
    [TestCase("0 1px 2px rgb(var(--x))")]
    [TestCase("0 1px 2px rgb(15 23 42 / calc(1))")]
    [TestCase("0 1px 2px #000000")]
    [TestCase("0 1px 2px black")]
    [TestCase("inset 0 1px 2px rgb(0 0 0)")]
    [TestCase("0 1em 2px rgb(0 0 0)")]
    [TestCase("1px rgb(0 0 0)")]
    [TestCase("0 1px 2px")]
    [TestCase("0 1px 2px rgb(0 0 0) rgb(0 0 0)")]
    [TestCase("0 1px 2px rgb(0 0 0),")]
    [TestCase("0 1px 2px RGB(0 0 0)")]
    [TestCase("0 1px 2px rgb(0 0 0); background: red")]
    [TestCase("")]
    public void Manifest_WhenShadowHoldsAUrl_ShouldBeRefused(string shadow)
    {
        var manifest = SampleNode();
        manifest["shape"]!["shadow"] = shadow;

        var errors = ErrorsOf(manifest);

        errors.Count.ShouldBe(1);
        errors[0].Path.ShouldBe("shape.shadow");
    }

    [Test]
    public void Manifest_WhenShadowIsABuiltInsValue_ShouldPass()
    {
        var builtIns = new TemplateCatalogue().All.Select(template => template.Shape.Shadow).ToList();
        builtIns.ShouldNotBeEmpty();

        foreach (var shadow in builtIns.Append("none").Append("0 0 0 1px rgb(15, 23, 42), -2px .5px 4px 1.5px rgb(0 0 0 / 50%)"))
        {
            var manifest = SampleNode();
            manifest["shape"]!["shadow"] = shadow;

            ErrorsOf(manifest).ShouldBeEmpty(shadow);
        }
    }

    [TestCase("rgb(47 91 234)")]
    [TestCase("#fff")]
    [TestCase("#2f5beaff")]
    [TestCase("#2f5bex")]
    [TestCase("red")]
    [TestCase("var(--sw-accent)")]
    [TestCase("#2f5bea url(x)")]
    public void Manifest_WhenAPaletteValueIsNotSixDigitHex_ShouldBeRefused(string colour)
    {
        var token = SampleNode();
        token["palette"]!["night-glow"] = colour;
        var extraColour = SampleNode();
        extraColour["palette"]!["extras"]![0]!["colour"] = colour;
        var extraInk = SampleNode();
        extraInk["palette"]!["extras"]![1]!["ink"] = colour;

        ErrorsOf(token).Select(error => error.Path).ShouldBe(["palette.night-glow"]);
        ErrorsOf(extraColour).Select(error => error.Path).ShouldBe(["palette.extras[0].colour"]);
        ErrorsOf(extraInk).Select(error => error.Path).ShouldBe(["palette.extras[1].ink"]);
    }

    [Test]
    public void Manifest_WhenAPaletteKeyIsMissing_ShouldBeRefused()
    {
        var manifest = SampleNode();
        manifest["palette"]!.AsObject().Remove("accent-2");

        ErrorsOf(manifest).ShouldBe([new ManifestError("palette.accent-2", "is required")]);
    }

    [Test]
    public void Manifest_WhenValuesAreOutsideTheSchema_ShouldNameEachPath()
    {
        var manifest = SampleNode();
        manifest["name"] = "Northwind Kickoff";
        manifest["chrome"]!["topBar"] = "ink";
        manifest["fonts"]![0]!["weight"] = 950;
        manifest["fonts"]![2]!["role"] = "display";
        manifest["fonts"]![3]!["file"] = new string('a', 201);
        manifest["titleArt"]!["kind"] = "video";
        manifest["palette"]!["extras"]![0]!["name"] = "extra-5";
        manifest["layouts"] = "title";
        manifest.Remove("lockPalette");

        ErrorsOf(manifest).Select(error => error.Path).ShouldBe(
            ["name", "layouts", "chrome.topBar", "fonts[0].weight", "fonts[2].role", "fonts[3].file", "titleArt.kind", "palette.extras[0].name", "lockPalette"]);
    }

    [TestCase("[]")]
    [TestCase("\"template\"")]
    [TestCase("{")]
    public void Manifest_WhenItIsNotAnObject_ShouldBeOneError(string json)
    {
        var parsed = ManifestJson.Parse(json);

        parsed.Manifest.ShouldBeNull();
        parsed.Errors.Count.ShouldBe(1);
        parsed.Errors[0].Path.ShouldBe("");
    }

    [Test]
    public void Manifest_WhenEverythingIsWrong_ShouldStopAtTwentyErrors()
    {
        var manifest = new JsonObject();
        for (var i = 0; i < 100; i++)
        {
            manifest["field" + i] = i;
        }

        ErrorsOf(manifest).Count.ShouldBe(20);
    }

    [Test]
    public void PaletteKeys_WhenComparedWithABuiltInPalette_ShouldBeItsTokensPlusAccent2()
    {
        var builtIn = new PaletteCatalogue().All[0].Tokens.Keys;

        ManifestPalette.Keys.Count.ShouldBe(42);
        ManifestPalette.Keys.ShouldBe(builtIn.Append("accent-2").Distinct(), ignoreOrder: true);
    }

    [Test]
    public void Sample_WhenChecked_ShouldMatchTheSchema()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("template-manifest.schema.json")));
        using var sample = JsonDocument.Parse(Sample());
        var problems = new List<string>();

        Check(schema.RootElement, schema.RootElement, sample.RootElement, "", problems);

        problems.ShouldBeEmpty();
        schema.RootElement.GetProperty("$schema").GetString().ShouldBe("https://json-schema.org/draft/2020-12/schema");
        schema.RootElement.GetProperty("properties").GetProperty("palette").GetProperty("required").EnumerateArray()
            .Select(key => key.GetString()).ShouldBe(ManifestPalette.Keys);
    }

    // The schema carries the rules ManifestJson.Parse holds a CSS-bound value to: a value Parse refuses fails the
    // schema at the same place, and the sample passes both.
    [TestCase("type/head", "url(x)")]
    [TestCase("type/body", "Northwind Sans, serif")]
    [TestCase("fonts/0/family", " Sans")]
    [TestCase("type/headTracking", "1px")]
    [TestCase("type/headTracking", "-0.0000000025em")]
    [TestCase("customLayouts/0/name", "Stage")]
    [TestCase("customLayouts/0/name", "a")]
    [TestCase("titleArt/generator", "url(x)")]
    [TestCase("titleArt/generator", "a-generator-name-that-is-forty-character")]
    [TestCase("ornament/dots/0", "var(--x)")]
    [TestCase("ornament/dots", null)]
    public void Schema_WhenAValueBreaksAParserRule_ShouldFailWhereParseDoes(string pointer, string? value)
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("template-manifest.schema.json")));
        var parts = pointer.Split('/');
        var changed = JsonNode.Parse(Sample())!;
        var parent = parts[..^1].Aggregate(changed, (node, part) => int.TryParse(part, out var index) ? node[index]! : node[part]!);

        // null stands for one dot more than the limit.
        JsonNode replacement = value is null
            ? new JsonArray(Enumerable.Repeat("accent", TemplateLimits.MAX_ORNAMENT_DOTS + 1).Select(dot => (JsonNode)dot).ToArray())
            : value;
        if (int.TryParse(parts[^1], out var at))
        {
            parent[at] = replacement;
        }
        else
        {
            parent[parts[^1]] = replacement;
        }

        var problems = new List<string>();
        using var document = JsonDocument.Parse(changed.ToJsonString());
        Check(schema.RootElement, schema.RootElement, document.RootElement, "", problems);

        problems.Count.ShouldBe(1);
        problems[0].ShouldStartWith("/" + pointer + ":");
        ManifestJson.Parse(changed.ToJsonString()).Errors.Select(error => error.Path.Replace("[", ".").Replace("]", "").Replace('.', '/')).ShouldBe([pointer]);
    }

    [Test]
    public void Sample_WhenAFieldIsAddedAnywhere_ShouldFailTheSchema()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("template-manifest.schema.json")));

        // Every object in the sample, the root included, refuses a field the schema does not name.
        foreach (var path in ObjectPaths(SampleNode(), ""))
        {
            var changed = SampleNode();
            At(changed, path)["surprise"] = 1;
            using var document = JsonDocument.Parse(changed.ToJsonString());
            var problems = new List<string>();

            Check(schema.RootElement, schema.RootElement, document.RootElement, "", problems);

            problems.ShouldBe([path + "/surprise: not allowed"], path);
            ErrorsOf(changed).Count.ShouldBe(1, path);
        }
    }

    [Test]
    public void Sample_WhenRead_ShouldHoldNoNullAndOnlyNorthwind()
    {
        var sample = Sample();

        sample.ShouldNotContain("null");
        ManifestJson.Parse(sample).Manifest!.Label.ShouldStartWith("Northwind");
    }

    private static IEnumerable<string> ObjectPaths(JsonNode node, string path)
    {
        if (node is JsonObject obj)
        {
            yield return path;
            foreach (var (key, value) in obj)
            {
                foreach (var inner in ObjectPaths(value!, path + "/" + key))
                {
                    yield return inner;
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                foreach (var inner in ObjectPaths(array[i]!, path + "/" + i))
                {
                    yield return inner;
                }
            }
        }
    }

    private static JsonObject At(JsonNode root, string path)
    {
        var node = root;
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node is JsonArray array ? array[int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)]! : node[part]!;
        }

        return node.AsObject();
    }

    // A walk of the JSON Schema keywords this schema uses. A keyword it does not know fails the test, so the schema
    // cannot grow a rule that nothing checks.
    private static void Check(JsonElement root, JsonElement schema, JsonElement value, string path, List<string> problems)
    {
        foreach (var keyword in schema.EnumerateObject())
        {
            switch (keyword.Name)
            {
                case "$schema" or "$id" or "$defs" or "title" or "description":
                    break;
                case "$ref":
                    var target = root;
                    foreach (var part in keyword.Value.GetString()!.TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries))
                    {
                        target = target.GetProperty(part);
                    }

                    Check(root, target, value, path, problems);
                    break;
                case "type":
                    var kind = keyword.Value.GetString();
                    var matches = kind switch
                    {
                        "object" => value.ValueKind == JsonValueKind.Object,
                        "array" => value.ValueKind == JsonValueKind.Array,
                        "string" => value.ValueKind == JsonValueKind.String,
                        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                        _ => throw new NotSupportedException("type " + kind),
                    };
                    if (!matches)
                    {
                        problems.Add($"{path}: not {kind}");
                        return;
                    }

                    break;
                case "additionalProperties":
                    keyword.Value.ValueKind.ShouldBe(JsonValueKind.False, path);
                    foreach (var property in value.EnumerateObject().Where(property => !schema.GetProperty("properties").TryGetProperty(property.Name, out _)))
                    {
                        problems.Add($"{path}/{property.Name}: not allowed");
                    }

                    break;
                case "required":
                    foreach (var name in keyword.Value.EnumerateArray().Select(name => name.GetString()!).Where(name => !value.TryGetProperty(name, out _)))
                    {
                        problems.Add($"{path}/{name}: missing");
                    }

                    break;
                case "properties":
                    schema.GetProperty("additionalProperties").ValueKind.ShouldBe(JsonValueKind.False, path);
                    foreach (var property in keyword.Value.EnumerateObject())
                    {
                        if (value.TryGetProperty(property.Name, out var inner))
                        {
                            Check(root, property.Value, inner, path + "/" + property.Name, problems);
                        }
                    }

                    break;
                case "items":
                    var index = 0;
                    foreach (var item in value.EnumerateArray())
                    {
                        Check(root, keyword.Value, item, path + "/" + index++, problems);
                    }

                    break;
                case "enum":
                    if (!keyword.Value.EnumerateArray().Any(allowed => allowed.GetString() == value.GetString()))
                    {
                        problems.Add($"{path}: not in the enum");
                    }

                    break;
                case "pattern":
                    if (!Regex.IsMatch(value.GetString()!, keyword.Value.GetString()!))
                    {
                        problems.Add($"{path}: does not match {keyword.Value.GetString()}");
                    }

                    break;
                case "maxItems":
                    if (value.GetArrayLength() > keyword.Value.GetInt32())
                    {
                        problems.Add($"{path}: too many items");
                    }

                    break;
                case "minLength":
                    if (value.GetString()!.Length < keyword.Value.GetInt32())
                    {
                        problems.Add($"{path}: too short");
                    }

                    break;
                case "maxLength":
                    if (value.GetString()!.Length > keyword.Value.GetInt32())
                    {
                        problems.Add($"{path}: too long");
                    }

                    break;
                case "minimum":
                    if (value.GetInt64() < keyword.Value.GetInt64())
                    {
                        problems.Add($"{path}: too small");
                    }

                    break;
                case "maximum":
                    if (value.GetInt64() > keyword.Value.GetInt64())
                    {
                        problems.Add($"{path}: too large");
                    }

                    break;
                default:
                    throw new NotSupportedException($"The schema uses '{keyword.Name}' at {path}, which this test does not check.");
            }
        }
    }
}
