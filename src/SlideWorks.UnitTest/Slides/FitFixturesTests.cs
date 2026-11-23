using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// src/contracts/fit-fixtures holds one file per layout, <layout>.json, each an object of named fixtures
// for that layout. A fixture is the most text the validator accepts for a layout case, in
// Title Case and, for the card and data layouts, in prose too. The client's CT fit tests render the same
// fixtures and check they stay on the slide, so this test is what ties those renders to the validator:
// each fixture is accepted, and one more word in any of its "grow" fields (JSON pointers into slots) is
// refused. "refusedBy" says what refuses it: "fit", the layout's fit rule (LayoutRules and SlideFit), or
// "length", a field's character limit, for the layouts with no fit rule (numbered-cards, cards, image,
// chart, gantt) and so filled to every limit. A fixture with "accepted": false is past the maximum and
// is refused as it stands.
[TestFixture]
public class FitFixturesTests
{
    private const string ONE_MORE_WORD = " Work";

    // The names fit-fixtures.json held before it was split into one file per layout.
    private static readonly string[] _oldNames =
    [
        "bulletsEight", "bulletsCallout", "twoColumnItems", "twoColumnText", "practice", "compare",
        "numberedCardsProse", "numberedCardsTitleCase", "numberedCardsWide",
        "rulesProse", "rulesTitleCase", "rulesWide", "cardsProse", "cardsTitleCase", "cardsWide",
        "tableTitle", "tableProse", "chartTitle", "chartProse", "ganttTitle", "ganttProse",
        "imageTitle", "imageProse", "joinKicker", "pollEight", "qaTitle",
    ];

    private static readonly Lazy<JsonObject> _fixtures = new(ReadFolder);

    // Accepted fixtures that no longer grow a field, by the layout bug whose wave-1 lane restores one. Under the
    // per-character estimate (SW-802) the fit rule and the character limit no longer bind together on these, so one more
    // word is refused by fit before length. Remove an entry when its fixture grows again.
    private static readonly Dictionary<string, string> _knownUngrown = new(StringComparer.Ordinal)
    {
        ["imageCallout"] = "SW-712",
        ["statementMaxWide"] = "SW-721",
    };

    // Every fixture of every file in the folder, by name. A name in two files throws, and so does a
    // fixture whose "layout" is not its file's name.
    private static JsonObject ReadFolder()
    {
        var all = new JsonObject();
        foreach (var path in ContractFiles.FilesIn("fit-fixtures"))
        {
            var layout = Path.GetFileNameWithoutExtension(path);
            foreach (var (name, fixture) in JsonNode.Parse(File.ReadAllText(path))!.AsObject())
            {
                if (fixture!["layout"]!.GetValue<string>() != layout)
                {
                    throw new InvalidDataException($"fit-fixtures/{layout}.json holds {name}, a fixture for another layout");
                }

                all.Add(name, fixture.DeepClone());
            }
        }

        return all;
    }

    private static IEnumerable<TestCaseData> Fixtures(bool accepted) =>
        _fixtures.Value.Where(f => IsAccepted(f.Value!) == accepted)
            .Select(f => new TestCaseData(f.Key).SetArgDisplayNames(f.Key));

    private static IEnumerable<TestCaseData> AcceptedFixtures() => Fixtures(accepted: true);

    private static IEnumerable<TestCaseData> RefusedFixtures() => Fixtures(accepted: false);

    private static IEnumerable<TestCaseData> GrownFixtures() =>
        _fixtures.Value.SelectMany(f => f.Value!["grow"]!.AsArray()
            .Select(p => new TestCaseData(f.Key, p!.GetValue<string>()).SetArgDisplayNames(f.Key + p)));

    [Test]
    public void Fixtures_WhenReadFromTheFolder_ShouldCoverEveryOldName()
    {
        var names = _fixtures.Value.Select(f => f.Key).ToList();

        _oldNames.Length.ShouldBe(26);
        _oldNames.Where(name => !names.Contains(name)).ShouldBeEmpty();
    }

    [Test]
    public void Fixtures_WhenAccepted_ShouldEachGrowAtLeastOneField()
    {
        var ungrown = _fixtures.Value.Where(f => IsAccepted(f.Value!) && f.Value!["grow"]!.AsArray().Count == 0)
            .Select(f => f.Key);

        ungrown.Order(StringComparer.Ordinal).ShouldBe(_knownUngrown.Keys.Order(StringComparer.Ordinal));
    }

    [TestCaseSource(nameof(AcceptedFixtures))]
    public void Validate_WhenMaxFitFixture_ShouldAcceptIt(string name)
    {
        var fixture = _fixtures.Value[name]!;

        var result = Validate(fixture, fixture["slots"]!);

        result.Errors.ShouldBeEmpty();
    }

    [TestCaseSource(nameof(RefusedFixtures))]
    public void Validate_WhenFitFixturePastTheMax_ShouldRefuseIt(string name)
    {
        var fixture = _fixtures.Value[name]!;

        var result = Validate(fixture, fixture["slots"]!);

        result.Errors.ShouldHaveSingleItem().ShouldContain(RefusalOf(fixture), Case.Sensitive, $"{name} was not refused as expected");
    }

    [TestCaseSource(nameof(GrownFixtures))]
    public void Validate_WhenMaxFitFixtureHasOneMoreWord_ShouldRefuseIt(string name, string pointer)
    {
        var fixture = _fixtures.Value[name]!;
        var slots = fixture["slots"]!.DeepClone();
        var (parent, key) = Resolve(slots, pointer);
        if (parent is JsonArray array)
        {
            var index = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
            array[index] = array[index]!.GetValue<string>() + ONE_MORE_WORD;
        }
        else
        {
            parent[key] = parent[key]!.GetValue<string>() + ONE_MORE_WORD;
        }

        var result = Validate(fixture, slots);

        result.Errors.ShouldHaveSingleItem().ShouldContain(RefusalOf(fixture), Case.Sensitive, $"{name}{pointer} with one more word was not refused as expected");
    }

    private static bool IsAccepted(JsonNode fixture) => fixture["accepted"]?.GetValue<bool>() ?? true;

    // The text of the one error the fixture's "refusedBy" expects: the fit rule's "too much text to
    // fit" or a character limit's "at most N characters (got M)".
    private static string RefusalOf(JsonNode fixture) => fixture["refusedBy"]!.GetValue<string>() switch
    {
        "fit" => "too much text to fit",
        "length" => " characters (got ",
        var other => throw new InvalidOperationException($"unknown refusedBy '{other}'"),
    };

    private static SlideValidationResult Validate(JsonNode fixture, JsonNode slots)
    {
        using var document = JsonDocument.Parse(slots.ToJsonString());
        return SlideValidator.Validate(fixture["layout"]!.GetValue<string>(), document.RootElement);
    }

    // The node that holds the pointer's last segment, and that segment as a property name or index.
    private static (JsonNode Parent, string Key) Resolve(JsonNode root, string pointer)
    {
        var segments = pointer.TrimStart('/').Split('/');
        var node = root;
        foreach (var segment in segments[..^1])
        {
            node = node is JsonArray array ? array[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)]! : node[segment]!;
        }

        return (node, segments[^1]);
    }
}
