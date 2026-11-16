using System.Text.RegularExpressions;
using SlideWorks.Core.Look;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

public class PaletteCatalogueTests
{
    // L1's key list, frozen with T0.1's first push (dd-on-night added by its literal audit).
    private static readonly string[] _keys =
    [
        "paper", "card", "chip", "grid", "ink", "muted",
        "accent", "accent-dark", "accent-light", "accent-glow", "accent-past", "on-accent",
        "good", "good-soft", "good-ink", "bad", "bad-soft", "bad-ink", "warn", "warn-soft", "warn-ink", "dd", "on-dd", "dd-on-night",
        "night", "night-ink", "night-2", "night-muted", "night-line", "night-glow", "number-from", "number-to", "stamp",
        "panel-accent", "panel-accent-ink", "panel-positive", "panel-positive-ink", "panel-negative", "panel-negative-ink",
        "panel-inverse", "panel-inverse-ink",
    ];

    private static readonly Regex _colour = new(@"^(#[0-9a-f]{6}|rgb\(\d{1,3} \d{1,3} \d{1,3}\))$", RegexOptions.None, TimeSpan.FromSeconds(1));

    [Test]
    public void EveryPaletteHasExactlyTheTokenKeys_WhenBuiltIn_ShouldMatchL1()
    {
        var catalogue = new PaletteCatalogue();

        catalogue.All.Select(p => p.Name).ShouldBe(["cobalt", "ember", "plum", "midnight"]);
        catalogue.Default.Name.ShouldBe("cobalt");
        foreach (var palette in catalogue.All)
        {
            palette.Tokens.Keys.Order(StringComparer.Ordinal).ShouldBe(_keys.Order(StringComparer.Ordinal), palette.Name);
        }
    }

    [Test]
    public void EveryTokenIsACompleteCssColour_WhenBuiltIn_ShouldParse()
    {
        var problems = new PaletteCatalogue().All
            .SelectMany(p => p.Tokens.Select(t => (Palette: p.Name, t.Key, t.Value)).Concat(p.Swatch.Select(s => (Palette: p.Name, Key: "swatch", Value: s))))
            .Where(t => !_colour.IsMatch(t.Value) || !Contrast.IsColour(t.Value))
            .Select(t => $"{t.Palette}.{t.Key} = '{t.Value}'");

        problems.ShouldBeEmpty();
    }

    [Test]
    public void Find_WhenCobalt_ShouldEqualTokensCssForEveryKeyItHas()
    {
        var css = File.ReadAllText(Path.Combine(Path.GetDirectoryName(ContractFiles.PathOf("x"))!, "..", "SlideWorks.Client", "src", "theme", "tokens.css"));
        var declared = Regex.Matches(css, @"--sw-([a-z0-9-]+):\s*(#[0-9a-fA-F]{3,8})\s*;", RegexOptions.None, TimeSpan.FromSeconds(1))
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.ToLowerInvariant());
        var cobalt = new PaletteCatalogue().Find("cobalt")!;

        declared.Keys.ShouldNotBeEmpty();
        declared.Where(d => cobalt.Tokens.ContainsKey(d.Key) && cobalt.Tokens[d.Key] != d.Value)
            .Select(d => $"{d.Key}: tokens.css {d.Value}, cobalt {cobalt.Tokens[d.Key]}")
            .ShouldBeEmpty();
    }

    [Test]
    public void Find_WhenUnknown_ShouldBeNull()
    {
        new PaletteCatalogue().Find("wada-1").ShouldBeNull();
    }
}
