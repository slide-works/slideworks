using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// src/contracts/fit-fixtures.json holds the most text the validator accepts for each content layout
// case. The client's CT fit tests render the same fixtures and check they stay on the slide, so this
// test is what ties those renders to the validator: each fixture is accepted, and one more word in any
// of its "grow" fields (JSON pointers into slots) is refused. "refusedBy" says what refuses it: "fit",
// the layout's fit rule (LayoutRules and SlideFit), or "length", a field's character limit, for the
// layouts with no fit rule (numbered-cards, cards) and so filled to every limit. A fixture with
// "accepted": false is past the maximum and is refused as it stands.
[TestFixture]
public class FitFixturesTests
{
    private const string ONE_MORE_WORD = " Work";

    private static readonly Lazy<JsonObject> _fixtures = new(() =>
        JsonNode.Parse(File.ReadAllText(ContractFiles.PathOf("fit-fixtures.json")))!.AsObject());

    private static IEnumerable<TestCaseData> Fixtures(bool accepted) =>
        _fixtures.Value.Where(f => IsAccepted(f.Value!) == accepted)
            .Select(f => new TestCaseData(f.Key).SetArgDisplayNames(f.Key));

    private static IEnumerable<TestCaseData> AcceptedFixtures() => Fixtures(accepted: true);

    private static IEnumerable<TestCaseData> RefusedFixtures() => Fixtures(accepted: false);

    private static IEnumerable<TestCaseData> GrownFixtures() =>
        _fixtures.Value.SelectMany(f => f.Value!["grow"]!.AsArray()
            .Select(p => new TestCaseData(f.Key, p!.GetValue<string>()).SetArgDisplayNames(f.Key + p)));

    [Test]
    public void Fixtures_WhenAccepted_ShouldEachGrowAtLeastOneField()
    {
        var ungrown = _fixtures.Value.Where(f => IsAccepted(f.Value!) && f.Value!["grow"]!.AsArray().Count == 0)
            .Select(f => f.Key);

        ungrown.ShouldBeEmpty();
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
