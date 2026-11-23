using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// Pins each layout's fit limit to the error that enforces it (SW-649). A layout is fit-controlled when
// its rule estimates the body's height (SlideFit.Check, seen as the result's Regions) and
// length-controlled when only character limits bound it. Every layout needs a fixture; a fixture's
// one-step-over variant must be refused by its own kind's error and never the other kind's, so a
// fixture can't pass as a fit maximum while a character limit is what really caps it. A fixture that
// disagrees with the server is listed below with its Bug key, never skipped.
[TestFixture]
public class FitFixtureLimitsTests
{
    private const string ONE_MORE_WORD = " Work";
    private const string FIT_ERROR = "too much text to fit";
    private const string LENGTH_ERROR = " characters (got ";

    // The layouts whose rule estimates the body's height with SlideFit.Check.
    private static readonly string[] _fitControlled =
    [
        "bars", "bullets", "cards", "columns-list", "compare", "flow", "image", "levels", "links", "loop",
        "numbered-cards", "practice", "quote", "rules", "stack", "table", "timeline", "two-column",
    ];

    // Layouts with no fixture file yet. Each entry names the Bug that adds it.
    private static readonly IReadOnlyDictionary<string, string> _knownWithoutFixture = new Dictionary<string, string>
    {
        ["title"] = "SW-723",
        ["close"] = "SW-723",
    };

    // Fit-controlled layouts whose fixtures only grow fields with a character limit, so no fixture proves
    // the height estimate's maximum. Each entry names the Bug that adds one.
    private static readonly IReadOnlyDictionary<string, string> _knownWithoutFitFixture = new Dictionary<string, string>
    {
        ["columns-list"] = "SW-724",
        ["loop"] = "SW-724",
        ["timeline"] = "SW-724",
    };

    // Length-controlled layouts whose own rule still refuses with "too much text to fit", so a fixture of theirs
    // is refused by fit though no height region is recorded (statement's sub line count).
    private static readonly HashSet<string> _refusesByFitWording = new(StringComparer.Ordinal) { "statement" };

    // Fit-controlled layouts that record their height only when it overflows, so an accepted slide reports
    // no fit region. Each entry names the Bug that makes them record it every time, as SlideFit.Check says.
    private static readonly IReadOnlyDictionary<string, string> _knownMeasuredOnlyWhenOver = new Dictionary<string, string>();

    // The region each layout's "estimate" describes: the part of the path after "<layout>.". A layout that
    // records several regions (stack's segments and points, practice's do and comes_back) names the one its
    // fixtures fill. A region the server does not record counts as height 0.
    private static readonly IReadOnlyDictionary<string, string> _estimateRegionByLayout = new Dictionary<string, string>
    {
        ["bars"] = "bars",
        ["bullets"] = "items",
        ["compare"] = "failure",
        ["flow"] = "nodes",
        ["image"] = "points",
        ["levels"] = "levels.items",
        ["links"] = "links",
        ["numbered-cards"] = "cards[0]",
        ["practice"] = "comes_back",
        ["quote"] = "quote",
        ["rules"] = "rules",
        ["stack"] = "segments",
        ["table"] = "rows",
        ["two-column"] = "left",
    };

    // Fixtures that fill a different region from their layout's default: name → region.
    private static readonly IReadOnlyDictionary<string, string> _estimateRegionByFixture = new Dictionary<string, string>();

    // Fixtures whose "estimate" does not match the height of the region they are pinned to: name → (that
    // region's height today, 0 when the server records none, and the Bug that corrects the fixture or the layout).
    private static readonly IReadOnlyDictionary<string, (string Region, double Height, string Bug)> _knownMisfits =
        new Dictionary<string, (string Region, double Height, string Bug)>();

    private static readonly Lazy<IReadOnlyDictionary<string, JsonObject>> _byLayout = new(ReadFolder);

    private static IReadOnlyDictionary<string, JsonObject> ReadFolder() =>
        ContractFiles.FilesIn("fit-fixtures").ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            path => JsonNode.Parse(File.ReadAllText(path))!.AsObject());

    private static IEnumerable<(string Name, JsonNode Fixture)> All() =>
        _byLayout.Value.Values.SelectMany(file => file.Select(f => (f.Key, f.Value!)));

    private static IEnumerable<TestCaseData> Layouts() =>
        LayoutCatalogue.LayoutNames.Select(name => new TestCaseData(name).SetArgDisplayNames(name));

    private static IEnumerable<TestCaseData> GrownFixtures() =>
        All().Where(f => IsAccepted(f.Fixture)).SelectMany(f => f.Fixture["grow"]!.AsArray()
            .Select(p => new TestCaseData(f.Name, p!.GetValue<string>()).SetArgDisplayNames(f.Name + p)));

    private static IEnumerable<TestCaseData> EstimatedFixtures() =>
        All().Where(f => f.Fixture["estimate"] is not null)
            .Select(f => new TestCaseData(f.Name).SetArgDisplayNames(f.Name));

    [Test]
    public void FitControlled_WhenComparedWithTheCatalogue_ShouldNameOnlyRealLayouts()
    {
        _fitControlled.Where(layout => !LayoutCatalogue.LayoutNames.Contains(layout)).ShouldBeEmpty();
        _knownWithoutFitFixture.Keys.Where(layout => !_fitControlled.Contains(layout)).ShouldBeEmpty();
        _knownMeasuredOnlyWhenOver.Keys.Where(layout => !_fitControlled.Contains(layout)).ShouldBeEmpty();
    }

    [TestCaseSource(nameof(Layouts))]
    public void Fixtures_WhenLayoutIsInTheCatalogue_ShouldExist(string layout)
    {
        var hasFixture = _byLayout.Value.TryGetValue(layout, out var fixtures) && fixtures.Count > 0;

        hasFixture.ShouldBe(!_knownWithoutFixture.ContainsKey(layout),
            _knownWithoutFixture.TryGetValue(layout, out var bug)
                ? $"{layout} is listed under {bug} as having no fit fixture; remove it from the list"
                : $"{layout} has no fit fixture in src/contracts/fit-fixtures; add {layout}.json");
    }

    [TestCaseSource(nameof(Layouts))]
    public void MaxFixtures_WhenValidated_ShouldBeAcceptedAndMeasuredOnlyByAFitControlledRule(string layout)
    {
        var accepted = Fixtures(layout).Where(f => IsAccepted(f.Fixture)).ToList();
        if (_knownWithoutFixture.ContainsKey(layout))
        {
            accepted.ShouldBeEmpty();
            return;
        }

        var results = accepted.Select(f => (f.Name, Result: Validate(f.Fixture, f.Fixture["slots"]!))).ToList();

        accepted.ShouldNotBeEmpty($"{layout} has no accepted fixture");
        results.Where(r => !r.Result.IsValid).Select(r => $"{r.Name}: {r.Result.Error}").ShouldBeEmpty();
        results.Any(r => r.Result.Regions.Count > 0).ShouldBe(_fitControlled.Contains(layout) && !_knownMeasuredOnlyWhenOver.ContainsKey(layout),
            _fitControlled.Contains(layout)
                ? $"{layout} is fit-controlled but no fixture recorded a height estimate"
                : $"{layout} is length-controlled: it has no fit estimate, yet a fixture recorded one");
    }

    [TestCaseSource(nameof(Layouts))]
    public void RefusedBy_WhenLayoutKindIsKnown_ShouldMatchTheKind(string layout)
    {
        // A fixture that only grows its headline or kicker proves the heading rule (SW-818: the headline's line budget
        // at the floor, the kicker's one row), which every layout has, not the layout's own body height.
        var refusals = Fixtures(layout).Where(f => !GrowsOnlyItsHeading(f.Fixture))
            .Select(f => f.Fixture["refusedBy"]!.GetValue<string>()).ToList();
        var fitControlled = _fitControlled.Contains(layout);

        refusals.Where(r => r is not "fit" and not "length").ShouldBeEmpty();
        refusals.Contains("fit").ShouldBe((fitControlled && !_knownWithoutFitFixture.ContainsKey(layout)) || _refusesByFitWording.Contains(layout),
            fitControlled
                ? $"{layout} is fit-controlled: a fixture must prove its height maximum (refusedBy \"fit\"), or the layout is listed with a Bug"
                : $"{layout} is length-controlled: it has no fit estimate, so every fixture is refused by a character limit");
    }

    private static bool GrowsOnlyItsHeading(JsonNode fixture)
    {
        var grow = fixture["grow"]!.AsArray().Select(p => p!.GetValue<string>()).ToList();
        return grow.Count > 0 && grow.All(p => p is "/headline" or "/kicker");
    }

    // Layouts whose fit check runs per card or panel, so one step over can refuse several at once.
    private static readonly HashSet<string> _fitPerItem = new(StringComparer.Ordinal) { "cards", "compare" };

    [TestCaseSource(nameof(GrownFixtures))]
    public void Validate_WhenFixtureGrowsOneStepOver_ShouldRefuseWithItsKindsErrorOnly(string name, string pointer)
    {
        var fixture = All().Single(f => f.Name == name).Fixture;
        var slots = Grown(fixture["slots"]!, pointer);
        var byFit = fixture["refusedBy"]!.GetValue<string>() == "fit";

        var result = Validate(fixture, slots);

        // Cards and compare check every card or panel, so a fit refusal there names each one over its budget (identical
        // cards give one error each); every other layout refuses with a single error.
        var perItem = byFit && _fitPerItem.Contains(fixture["layout"]!.GetValue<string>());
        var errors = perItem ? result.Errors.ToList() : [result.Errors.ShouldHaveSingleItem($"{name}{pointer} one word over")];
        errors.ShouldNotBeEmpty($"{name}{pointer} one word over");
        if (perItem)
        {
            errors.Count.ShouldBe(result.Regions.Count(r => r.Height > r.Budget),
                $"{name}{pointer}: one fit error per card or panel over its budget");
        }
        foreach (var error in errors)
        {
            error.ShouldContain(byFit ? FIT_ERROR : LENGTH_ERROR, Case.Sensitive, $"{name}{pointer} one word over: {error}");
            error.ShouldNotContain(byFit ? LENGTH_ERROR : FIT_ERROR, Case.Sensitive, $"{name}{pointer} one word over: {error}");
        }

        result.Regions.Any(r => r.Height > r.Budget).ShouldBe(errors.Any(error => error.Contains(FIT_ERROR + " on the slide", StringComparison.Ordinal)),
            $"{name}{pointer}: an overflowing height region goes with the slide's fit error");
    }

    // "estimate" is the height of the one region the fixture fills (the tallest of a stacked layout's regions
    // is not always it), so it is compared with that region alone, found by the maps above.
    [TestCaseSource(nameof(EstimatedFixtures))]
    public void Estimate_WhenFixtureHasOne_ShouldBeTheHeightOfItsOwnRegion(string name)
    {
        var fixture = All().Single(f => f.Name == name).Fixture;
        var layout = fixture["layout"]!.GetValue<string>();
        if (!_estimateRegionByFixture.TryGetValue(name, out var key))
        {
            _estimateRegionByLayout.TryGetValue(layout, out key);
        }

        key.ShouldNotBeNull($"fixture {name} ({layout}) has an estimate but no region is mapped for it; add it to the test's region maps");
        var path = $"{layout}.{key}";

        var result = Validate(fixture, fixture["slots"]!);

        var height = Math.Round(result.Regions.SingleOrDefault(r => r.Path == path)?.Height ?? 0, 1);
        var regions = string.Join(", ", result.Regions.Select(r => $"{r.Path} {r.Height:0.0}/{r.Budget:0.0}"));
        var estimate = fixture["estimate"]!.GetValue<double>();
        if (_knownMisfits.TryGetValue(name, out var misfit))
        {
            path.ShouldBe(misfit.Region, $"{name} is listed under {misfit.Bug} against another region");
            height.ShouldBe(misfit.Height, 0.05, $"{name} is listed under {misfit.Bug}; regions: {regions}");
            Math.Abs(height - estimate).ShouldBeGreaterThan(0.05,
                $"{name} now matches its estimate on {path}; remove it from the {misfit.Bug} list");
        }
        else
        {
            height.ShouldBe(estimate, 0.05, $"{name}: estimate {estimate} is not the height of {path}; regions: {regions}");
        }
    }

    private static IEnumerable<(string Name, JsonNode Fixture)> Fixtures(string layout) =>
        _byLayout.Value.TryGetValue(layout, out var file) ? file.Select(f => (f.Key, f.Value!)) : [];

    private static bool IsAccepted(JsonNode fixture) => fixture["accepted"]?.GetValue<bool>() ?? true;

    private static SlideValidationResult Validate(JsonNode fixture, JsonNode slots)
    {
        using var document = JsonDocument.Parse(slots.ToJsonString());
        var surface = fixture["surface"]?.GetValue<string>();
        return surface is null
            ? SlideValidator.Validate(fixture["layout"]!.GetValue<string>(), document.RootElement)
            : SlideValidator.Validate(LayoutCatalogue.Find(fixture["layout"]!.GetValue<string>())!, document.RootElement, FitWidths.Default, surface);
    }

    // The slots with one more word at the pointer.
    private static JsonNode Grown(JsonNode slots, string pointer)
    {
        var copy = slots.DeepClone();
        var segments = pointer.TrimStart('/').Split('/');
        var node = copy;
        foreach (var segment in segments[..^1])
        {
            node = node is JsonArray array ? array[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)]! : node[segment]!;
        }

        if (node is JsonArray items)
        {
            var index = int.Parse(segments[^1], System.Globalization.CultureInfo.InvariantCulture);
            items[index] = items[index]!.GetValue<string>() + ONE_MORE_WORD;
        }
        else
        {
            node[segments[^1]] = node[segments[^1]]!.GetValue<string>() + ONE_MORE_WORD;
        }

        return copy;
    }
}
