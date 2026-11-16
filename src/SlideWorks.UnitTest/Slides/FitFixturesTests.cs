using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// src/contracts/fit-fixtures.json holds the most Title Case text the fit check accepts for each content
// layout case. The client's CT fit tests render the same fixtures and check they stay on the slide, so
// this test is what ties those renders to the validator: each fixture is accepted, and one more word in
// any of its "grow" fields (JSON pointers into slots) is refused.
[TestFixture]
public class FitFixturesTests
{
    private const string ONE_MORE_WORD = " Work";

    private static readonly Lazy<JsonObject> _fixtures = new(() =>
        JsonNode.Parse(File.ReadAllText(ContractFiles.PathOf("fit-fixtures.json")))!.AsObject());

    private static IEnumerable<TestCaseData> Fixtures() =>
        _fixtures.Value.Select(f => new TestCaseData(f.Key).SetArgDisplayNames(f.Key));

    private static IEnumerable<TestCaseData> GrownFixtures() =>
        _fixtures.Value.SelectMany(f => f.Value!["grow"]!.AsArray()
            .Select(p => new TestCaseData(f.Key, p!.GetValue<string>()).SetArgDisplayNames(f.Key + p)));

    [TestCaseSource(nameof(Fixtures))]
    public void Validate_WhenMaxFitFixture_ShouldAcceptIt(string name)
    {
        var fixture = _fixtures.Value[name]!;

        var result = Validate(fixture, fixture["slots"]!);

        result.Errors.ShouldBeEmpty();
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

        result.IsValid.ShouldBeFalse($"{name}{pointer} with one more word was accepted");
    }

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
