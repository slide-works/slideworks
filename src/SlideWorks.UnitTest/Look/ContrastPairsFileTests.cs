using System.Globalization;
using System.Text;
using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

// src/contracts/contrast-pairs.json is PaletteContrast.Pairs written out, so the template editor's instant check
// uses the server's pairs. Rewrite it with UPDATE_CONTRAST_PAIRS=1.
public class ContrastPairsFileTests
{
    private const string FILE = "contrast-pairs.json";
    private static readonly string[] _members = ["name", "fore", "back", "needed", "over", "share"];

    [Test]
    public void File_WhenRead_ShouldEqualTheCode()
    {
        var path = ContractFiles.PathOf(FILE);
        if (Environment.GetEnvironmentVariable("UPDATE_CONTRAST_PAIRS") == "1")
        {
            File.WriteAllText(path, Text());
        }

        const string hint = "run with UPDATE_CONTRAST_PAIRS=1 to rewrite the file";
        File.Exists(path).ShouldBeTrue(hint);
        using var file = JsonDocument.Parse(File.ReadAllText(path));
        var entries = file.RootElement.EnumerateArray().ToList();

        entries.SelectMany(entry => entry.EnumerateObject()).Select(member => member.Name).Where(name => !_members.Contains(name)).ShouldBeEmpty(hint);
        entries.Select(Read).ShouldBe(PaletteContrast.Pairs, hint);
    }

    [Test]
    public void Pairs_WhenListed_ShouldBeTheCheckedPairsInOrder()
    {
        var checkedPairs = PaletteContrast.Check(new PaletteCatalogue().All[0]);

        PaletteContrast.Pairs.Select(pair => (pair.Name, pair.Needed)).ShouldBe(checkedPairs.Select(check => (check.Pair, check.Bar)));
        PaletteContrast.Pairs.ShouldAllBe(pair => (pair.Over == null) == (pair.Share == null));
        PaletteContrast.Pairs.Where(pair => pair.Share is not null).ShouldAllBe(pair => pair.Share > 0 && pair.Share <= 1);
        // The mixed-ground pairs: the three dark soft washes over night, and the pin on a solid accent panel over the panel.
        PaletteContrast.Pairs.Where(pair => pair.Share is not null).Select(pair => pair.Name).ShouldBe(
            ["dark-soft-accent", "dark-soft-positive", "dark-soft-negative", "pin-solid-accent"]);
        PaletteContrast.Pairs.Where(pair => pair.Name.StartsWith("dark-soft-", StringComparison.Ordinal)).Select(pair => (pair.Over, pair.Share))
            .ShouldBe([("night", 0.24), ("night", 0.22), ("night", 0.22)]);
        PaletteContrast.Pairs.Select(pair => pair.Name).ShouldBeUnique();
    }

    // One entry as the tuple of PaletteContrast.Pairs. An entry without over and share is a plain pair.
    private static (string Name, string Fore, string Back, double Needed, string? Over, double? Share) Read(JsonElement entry) =>
    (
        entry.GetProperty("name").GetString()!,
        entry.GetProperty("fore").GetString()!,
        entry.GetProperty("back").GetString()!,
        entry.GetProperty("needed").GetDouble(),
        entry.TryGetProperty("over", out var over) ? over.GetString() : null,
        entry.TryGetProperty("share", out var share) ? share.GetDouble() : null
    );

    // One object per pair on its own line, in the code's order; over and share are left out of a plain pair.
    private static string Text()
    {
        var lines = PaletteContrast.Pairs.Select(pair =>
        {
            var line = new StringBuilder().Append(CultureInfo.InvariantCulture, $"  {{\"name\": {Quoted(pair.Name)}, \"fore\": {Quoted(pair.Fore)}, \"back\": {Quoted(pair.Back)}, \"needed\": {pair.Needed:0.0###}");
            if (pair.Over is not null)
            {
                line.Append(CultureInfo.InvariantCulture, $", \"over\": {Quoted(pair.Over)}");
            }

            if (pair.Share is { } share)
            {
                line.Append(CultureInfo.InvariantCulture, $", \"share\": {share:0.0###}");
            }

            return line.Append('}').ToString();
        });
        return "[\n" + string.Join(",\n", lines) + "\n]\n";
    }

    private static string Quoted(string text) => JsonSerializer.Serialize(text);
}
