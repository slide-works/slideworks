using System.Text;
using System.Text.Json.Nodes;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

// What ManifestJson.Parse refuses because a manifest is an upload: a field given twice, a file over the size limit,
// and every text that ends up in CSS or in a path (font family names, type.headTracking, a custom layout's name,
// shape.shadow).
public class ManifestJsonRulesTests
{
    private static string Sample() => File.ReadAllText(ContractFiles.PathOf("template-manifest.sample.json"));

    private static JsonObject SampleNode() => JsonNode.Parse(Sample())!.AsObject();

    private static IReadOnlyList<ManifestError> ErrorsOf(JsonObject manifest) => ManifestJson.Parse(manifest.ToJsonString()).Errors;

    [TestCase("{", "{\"label\": \"Another\",")]
    [TestCase("\"palette\": {", "\"palette\": {\"paper\": \"#000000\",")]
    [TestCase("\"lockPalette\": true", "\"lockPalette\": true, \"lockPalette\": false")]
    [TestCase("\"role\": \"body\"", "\"role\": \"body\", \"role\": \"head\"")]
    public void Parse_WhenAKeyRepeats_ShouldRefuse(string find, string replace)
    {
        var sample = Sample();
        var at = sample.IndexOf(find, StringComparison.Ordinal);
        at.ShouldBeGreaterThanOrEqualTo(0);
        var repeated = sample[..at] + replace + sample[(at + find.Length)..];

        var parsed = ManifestJson.Parse(repeated);

        parsed.Manifest.ShouldBeNull();
        parsed.Errors.ShouldBe([new ManifestError("", "is not valid JSON, or gives a field twice")]);
    }

    [Test]
    public void Parse_WhenOverTheSizeLimit_ShouldRefuseBeforeParsing()
    {
        var sample = Sample();
        var atTheLimit = sample + new string(' ', ManifestJson.MAX_BYTES - Encoding.UTF8.GetByteCount(sample));
        var tooLarge = new ManifestError("", "is larger than 200000 bytes");

        // Not JSON at all: the size is what is reported, so nothing was parsed.
        ManifestJson.Parse(new string('[', ManifestJson.MAX_BYTES + 1)).Errors.ShouldBe([tooLarge]);
        ManifestJson.Parse(atTheLimit + " ").Errors.ShouldBe([tooLarge]);
        ManifestJson.Parse(atTheLimit).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Parse_WhenUnderTheLimitInCharactersButOverInBytes_ShouldRefuse()
    {
        // 120,000 two-byte characters: fewer characters than the limit, more bytes.
        var manifest = SampleNode();
        manifest["purpose"] = new string('é', 120_000);
        var text = manifest.ToJsonString(new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        text.Length.ShouldBeLessThan(ManifestJson.MAX_BYTES);
        ManifestJson.Parse(text).Errors.ShouldBe([new ManifestError("", "is larger than 200000 bytes")]);
    }

    [TestCase("Northwind \"Sans\"")]
    [TestCase("Northwind's")]
    [TestCase("Northwind Sans, serif")]
    [TestCase("x; color:red")]
    [TestCase("url(x)")]
    [TestCase("a\\62")]
    [TestCase("x}body{display:none")]
    [TestCase("a/b")]
    [TestCase("Sans\n")]
    [TestCase(" Sans")]
    [TestCase("Sans ")]
    [TestCase("-sans")]
    [TestCase("Sans Text")]
    [TestCase("Ｓａｎｓ")]
    [TestCase("")]
    [TestCase("Sans Sans Sans Sans Sans Sans Sans Sans Sans Sans Sans Sans12")]
    public void Check_WhenAFontFamilyHoldsCssPunctuation_ShouldRefuse(string family)
    {
        var head = SampleNode();
        head["type"]!["head"] = family;
        var body = SampleNode();
        body["type"]!["body"] = family;
        var font = SampleNode();
        font["fonts"]![2]!["family"] = family;

        ErrorsOf(head).Select(error => error.Path).ShouldBe(["type.head"]);
        ErrorsOf(body).Select(error => error.Path).ShouldBe(["type.body"]);
        ErrorsOf(font).Select(error => error.Path).ShouldBe(["fonts[2].family"]);
        ErrorsOf(head)[0].Message.ShouldBe("must be 1 to 60 characters: letters, digits, spaces and hyphens, starting and ending with a letter or digit");
    }

    [TestCase("Inter")]
    [TestCase("Plus Jakarta Sans")]
    [TestCase("Source Sans 3")]
    [TestCase("IBM Plex Sans-Condensed")]
    [TestCase("A")]
    [TestCase("Sans Sans Sans Sans Sans Sans Sans Sans Sans Sans Sans Sans1")]
    public void Check_WhenAFontFamilyIsPlainWords_ShouldPass(string family)
    {
        var manifest = SampleNode();
        manifest["type"]!["head"] = family;
        manifest["type"]!["body"] = family;
        manifest["fonts"]![0]!["family"] = family;

        ErrorsOf(manifest).ShouldBeEmpty();
    }

    [TestCase("../x")]
    [TestCase("a/b")]
    [TestCase("A")]
    [TestCase("")]
    [TestCase("a")]
    [TestCase("1ab")]
    [TestCase("-ab")]
    [TestCase("stage_ladder")]
    [TestCase("stage.ladder")]
    [TestCase("Stage-ladder")]
    [TestCase("stage-ladder\n")]
    [TestCase("stage ladder")]
    [TestCase("a\\b")]
    [TestCase("ab%2e%2e")]
    [TestCase("abcdefghijklmnopqrstuvwxyz-01234")]
    public void Check_WhenALayoutNameIsNotASlug_ShouldRefuse(string name)
    {
        var manifest = SampleNode();
        manifest["customLayouts"]![0]!["name"] = name;

        var errors = ErrorsOf(manifest);

        errors.Select(error => error.Path).ShouldBe(["customLayouts[0].name"]);
        errors[0].Message.ShouldBe("must be 2 to 31 characters: a lower-case letter, then lower-case letters, digits and hyphens");
    }

    [TestCase("ab")]
    [TestCase("stage-ladder")]
    [TestCase("abcdefghijklmnopqrstuvwxyz-0123")]
    public void Check_WhenALayoutNameIsASlug_ShouldPass(string name)
    {
        var manifest = SampleNode();
        manifest["customLayouts"]![0]!["name"] = name;

        ErrorsOf(manifest).ShouldBeEmpty();
    }

    [TestCase("url(x)")]
    [TestCase("1px; color: red")]
    [TestCase("calc(1em)")]
    [TestCase("5em")]
    [TestCase("0.21em")]
    [TestCase("-0.11em")]
    [TestCase("-0.025")]
    [TestCase("0px")]
    [TestCase("0.1EM")]
    [TestCase("0.1 em")]
    [TestCase(" 0.1em")]
    [TestCase("0.1em\n")]
    [TestCase("+0.1em")]
    [TestCase(".1em")]
    [TestCase("1.em")]
    [TestCase("1e-2em")]
    [TestCase("0.1rem")]
    [TestCase("--0.1em")]
    [TestCase("normal")]
    [TestCase("var(--x)")]
    [TestCase("0.0000000001em")]
    [TestCase("99999999999999999999999999999999999999999999999999em")]
    [TestCase("")]
    public void Check_WhenHeadTrackingIsNotALength_ShouldRefuse(string tracking)
    {
        var manifest = SampleNode();
        manifest["type"]!["headTracking"] = tracking;

        var errors = ErrorsOf(manifest);

        errors.Select(error => error.Path).ShouldBe(["type.headTracking"]);
        errors[0].Message.ShouldBe("must be 0, or a length in em from -0.1em to 0.2em, such as -0.025em");
    }

    [TestCase("0")]
    [TestCase("0em")]
    [TestCase("-0em")]
    [TestCase("-0.1em")]
    [TestCase("0.2em")]
    [TestCase("-0.025em")]
    [TestCase("0.200em")]
    [TestCase("00.05em")]
    public void Check_WhenHeadTrackingIsASmallEmLength_ShouldPass(string tracking)
    {
        var manifest = SampleNode();
        manifest["type"]!["headTracking"] = tracking;

        ErrorsOf(manifest).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenHeadTrackingIsABuiltInsValue_ShouldPass()
    {
        var builtIns = new SlideWorks.Core.Look.TemplateCatalogue().All.ToList();
        builtIns.ShouldNotBeEmpty();

        foreach (var template in builtIns)
        {
            ManifestJson.IsTracking(template.Type.HeadTracking).ShouldBeTrue(template.Name);
            ManifestJson.IsFamily(template.Type.Head).ShouldBeTrue(template.Name);
            ManifestJson.IsFamily(template.Type.Body).ShouldBeTrue(template.Name);
        }
    }

    [Test]
    public void Check_WhenTheShadowIsOverTwoHundredCharacters_ShouldRefuse()
    {
        // Each shadow is allowed on its own; only the length is wrong.
        const string one = "0 1px 2px rgb(15 23 42 / 0.06)";
        var atTheLimit = string.Join(", ", Enumerable.Repeat(one, 5));
        atTheLimit = atTheLimit + new string(' ', ManifestShadow.MAX_LENGTH - atTheLimit.Length - one.Length - 1) + "," + one;
        var manifest = SampleNode();
        manifest["shape"]!["shadow"] = atTheLimit;
        var over = SampleNode();
        over["shape"]!["shadow"] = " " + atTheLimit;

        atTheLimit.Length.ShouldBe(ManifestShadow.MAX_LENGTH);
        ErrorsOf(manifest).ShouldBeEmpty();
        ErrorsOf(over).Select(error => error.Path).ShouldBe(["shape.shadow"]);
    }

    [TestCase("0 1px 2px rgb(((((((((((((((((((((0 0 0)))))))))))))))))))))")]
    [TestCase("0 1px 2px rgb(rgb(rgb(rgb(rgb(0 0 0)))))")]
    [TestCase("((((((((((((((((((((((((((((((((((((((((")]
    [TestCase("0 1px 2px rgb(0 0 0))")]
    [TestCase("0 1px 2px (rgb(0 0 0))")]
    public void Check_WhenTheShadowNestsParentheses_ShouldRefuse(string shadow)
    {
        var manifest = SampleNode();
        manifest["shape"]!["shadow"] = shadow;

        ErrorsOf(manifest).Select(error => error.Path).ShouldBe(["shape.shadow"]);
    }
}
