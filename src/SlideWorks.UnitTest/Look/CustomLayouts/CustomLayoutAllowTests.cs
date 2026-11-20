using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The custom-layout allow-lists and limits (T10): CustomLayoutAllow.Default against
// src/contracts/custom-layout-allow.json, and its tokens against tokens.css and the palette keys.
public class CustomLayoutAllowTests
{
    // The extras (D11) are set per template by lookStyle, so tokens.css does not declare them.
    private static readonly string[] _extras =
    [
        "--sw-extra-1", "--sw-extra-2", "--sw-extra-3", "--sw-extra-4",
        "--sw-extra-1-ink", "--sw-extra-2-ink", "--sw-extra-3-ink", "--sw-extra-4-ink",
    ];

    // The token families the contract names beside the palette's colours.
    private static readonly string[] _numberFamilies = ["--sw-radius-", "--sw-space-", "--sw-slide-"];

    private static readonly Regex _declaration =
        new(@"^\s*(--sw-[a-z0-9-]+)\s*:([^;]*);", RegexOptions.Multiline, TimeSpan.FromSeconds(1));

    // A checked colour or a number with a unit: # and 6 hex digits, an rgb() of three numbers, or px.
    private static readonly Regex _checkedValue = new(
        @"^(#[0-9a-fA-F]{6}|rgb\(\s*\d+(\.\d+)?(\s*,\s*|\s+)\d+(\.\d+)?(\s*,\s*|\s+)\d+(\.\d+)?\s*\)|\d+(\.\d+)?px)$",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    [Test]
    public void Allow_WhenCompared_ShouldMatchTheContractFile()
    {
        var allow = CustomLayoutAllow.Default;

        var file = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            File.ReadAllText(ContractFiles.PathOf("custom-layout-allow.json")))!;

        file.Keys.ShouldBe(
            [
                "tags", "attributes", "slotTypes", "blockSlotTypes", "cssProperties", "cssFunctions", "cssUnits",
                "cssKeywords", "tokens", "fontTokens",
            ],
            Case.Sensitive);
        allow.Tags.ShouldBe(file["tags"], Case.Sensitive);
        allow.Attributes.ShouldBe(file["attributes"], Case.Sensitive);
        allow.SlotTypes.ShouldBe(file["slotTypes"], Case.Sensitive);
        allow.BlockSlotTypes.ShouldBe(file["blockSlotTypes"], Case.Sensitive);
        allow.CssProperties.ShouldBe(file["cssProperties"], Case.Sensitive);
        allow.CssFunctions.ShouldBe(file["cssFunctions"], Case.Sensitive);
        allow.CssUnits.ShouldBe(file["cssUnits"], Case.Sensitive);
        allow.CssKeywords.ShouldBe(file["cssKeywords"], Case.Sensitive);
        allow.Tokens.ShouldBe(file["tokens"], Case.Sensitive);
        allow.FontTokens.ShouldBe(file["fontTokens"], Case.Sensitive);
    }

    [Test]
    public void Allow_WhenRead_ShouldHoldTheContractsLists()
    {
        var allow = CustomLayoutAllow.Default;

        allow.Tags.ShouldBe(["div", "span", "p", "h2", "h3", "ul", "ol", "li", "b", "i", "em", "strong"], Case.Sensitive);
        allow.Attributes.ShouldBe(
            ["class", "data-slot", "data-each", "data-if", "data-var", "data-variant", "data-step"], Case.Sensitive);
        allow.SlotTypes.ShouldBe(["text", "number", "enum", "list", "image"], Case.Sensitive);
        allow.BlockSlotTypes.ShouldBe(["callout", "chip", "badge", "levels", "facts"], Case.Sensitive);
        allow.CssFunctions.ShouldBe(
            [
                "var", "calc", "min", "max", "clamp", "color-mix", "linear-gradient", "radial-gradient",
                "conic-gradient", "repeating-linear-gradient", "rotate", "translate", "repeat", "minmax",
            ],
            Case.Sensitive);
        allow.CssUnits.ShouldBe(["px", "em", "%", "fr", "deg"], Case.Sensitive);
        allow.FontTokens.ShouldBe(["--sw-font-head", "--sw-font-body", "--sw-font-mono"], Case.Sensitive);
        allow.CssProperties.Count.ShouldBe(67);
        allow.CssKeywords.Count.ShouldBe(46);
        Lists(allow).ShouldAllBe(list => list.Distinct(StringComparer.Ordinal).Count() == list.Count);
    }

    [Test]
    public void Allow_WhenRead_ShouldHoldNoFreeTextToken()
    {
        var declared = DeclaredTokens();
        var paletteTokens = PaletteTokens();

        var tokens = CustomLayoutAllow.Default.Tokens;

        // Every token outside the extras is declared in tokens.css with a checked colour or a px number.
        var unchecked_ = tokens
            .Except(_extras, StringComparer.Ordinal)
            .Where(name => !declared.TryGetValue(name, out var value) || !_checkedValue.IsMatch(value))
            .ToArray();
        unchecked_.ShouldBeEmpty();

        // And it is one the contract names: a palette colour or a radius, space or slide number.
        var unnamed = tokens
            .Except(_extras, StringComparer.Ordinal)
            .Except(paletteTokens, StringComparer.Ordinal)
            .Where(name => !_numberFamilies.Any(family => name.StartsWith(family, StringComparison.Ordinal)))
            .ToArray();
        unnamed.ShouldBeEmpty();

        // The whole of each number family is on the list, and no font token is.
        var families = declared.Keys
            .Where(name => _numberFamilies.Any(family => name.StartsWith(family, StringComparison.Ordinal)))
            .ToArray();
        families.ShouldNotBeEmpty();
        families.Except(tokens, StringComparer.Ordinal).ShouldBeEmpty();
        _extras.Except(tokens, StringComparer.Ordinal).ShouldBeEmpty();
        tokens.ShouldContain("--sw-accent-2");
        tokens.ShouldNotContain("--sw-lift");
        tokens.ShouldNotContain("--sw-night-surface");
        tokens.ShouldNotContain("--sw-info");
        tokens.Intersect(CustomLayoutAllow.Default.FontTokens, StringComparer.Ordinal).ShouldBeEmpty();
        tokens.Where(name => name.StartsWith("--sw-font", StringComparison.Ordinal)).ShouldBeEmpty();

        // The check itself refuses free text: these are declared in tokens.css and are not checked values.
        string[] freeText = ["--sw-lift", "--sw-night-surface", "--sw-info", "--sw-font-head", "--sw-font-body", "--sw-font-mono"];
        freeText.ShouldAllBe(name => declared.ContainsKey(name) && !_checkedValue.IsMatch(declared[name]));
    }

    [Test]
    public void Allow_WhenRead_ShouldHoldEveryPaletteKey()
    {
        var paletteTokens = PaletteTokens();

        var tokens = CustomLayoutAllow.Default.Tokens;

        paletteTokens.Length.ShouldBeGreaterThan(0);
        paletteTokens.Except(tokens, StringComparer.Ordinal).ShouldBeEmpty();
    }

    [Test]
    public void Allow_WhenRead_ShouldNotAllowVisibility()
    {
        var allow = CustomLayoutAllow.Default;

        allow.CssProperties.ShouldNotContain("visibility");
        allow.CssProperties.ShouldContain("opacity");
    }

    [Test]
    public void Limits_WhenRead_ShouldBeTheContractsNumbers()
    {
        CustomLayoutLimits.MAX_UPLOAD_BYTES.ShouldBe(800_000);
        CustomLayoutLimits.MAX_PART_BYTES.ShouldBe(262_144);
        CustomLayoutLimits.MAX_MARKUP_DEPTH.ShouldBe(12);
        CustomLayoutLimits.MAX_NODES.ShouldBe(400);
        CustomLayoutLimits.MAX_DRAWN_NODES.ShouldBe(2_000);
        CustomLayoutLimits.MAX_CLASSES_PER_ELEMENT.ShouldBe(8);
        CustomLayoutLimits.MAX_STEPS.ShouldBe(20);
        CustomLayoutLimits.MAX_CSS_RULES.ShouldBe(300);
        CustomLayoutLimits.MAX_DECLARATIONS_PER_RULE.ShouldBe(40);
        CustomLayoutLimits.MAX_PARENTHESIS_DEPTH.ShouldBe(6);
        CustomLayoutLimits.MAX_VALUE_LAYERS.ShouldBe(8);
        CustomLayoutLimits.MAX_FUNCTION_ARGUMENTS.ShouldBe(16);
        CustomLayoutLimits.MAX_SLOTS.ShouldBe(24);
        CustomLayoutLimits.MAX_ENUM_VALUE_CHARS.ShouldBe(40);
    }

    private static IReadOnlyList<IReadOnlyList<string>> Lists(CustomLayoutAllow allow) =>
    [
        allow.Tags, allow.Attributes, allow.SlotTypes, allow.BlockSlotTypes, allow.CssProperties, allow.CssFunctions,
        allow.CssUnits, allow.CssKeywords, allow.Tokens, allow.FontTokens,
    ];

    // Each --sw- custom property tokens.css declares, with its value on one line.
    private static Dictionary<string, string> DeclaredTokens()
    {
        var css = File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(ContractFiles.PathOf("palettes.json"))!, "..", "SlideWorks.Client", "src", "theme", "tokens.css"));

        return _declaration.Matches(css).ToDictionary(
            match => match.Groups[1].Value,
            match => Regex.Replace(match.Groups[2].Value, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim(),
            StringComparer.Ordinal);
    }

    // --sw-<key> for each token key of the first palette in palettes.json.
    private static string[] PaletteTokens()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("palettes.json")));

        return [.. file.RootElement.GetProperty("palettes")[0].GetProperty("tokens").EnumerateObject().Select(key => "--sw-" + key.Name)];
    }
}
