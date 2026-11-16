using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests;

[TestFixture]
public class JsonShapeTests
{
    private const string EXAMPLE = """
        { "id": "abc", "count": 2, "open": true, "lastChange": { "actor": "Sam" }, "rows": [{ "title": "One", "subtitle": "Sub" }] }
        """;

    [Test]
    public void AssertMatches_WhenShapeMatches_ShouldPass()
    {
        Should.NotThrow(() => Check("""
            { "id": "x", "count": 9, "open": false, "lastChange": { "actor": "Alex" }, "rows": [{ "title": "A", "subtitle": "B" }, { "title": "C", "subtitle": "D" }] }
            """));
    }

    [Test]
    public void AssertMatches_WhenNullAtAListedPath_ShouldPass()
    {
        Should.NotThrow(() => Check("""
            { "id": "x", "count": 9, "open": false, "lastChange": null, "rows": [{ "title": "A", "subtitle": null }] }
            """, "lastChange", "rows[].subtitle"));
    }

    [Test]
    public void AssertMatches_WhenAPropertyIsMissing_ShouldFail()
    {
        Fails("""
            { "id": "x", "count": 9, "lastChange": { "actor": "Alex" }, "rows": [] }
            """).ShouldContain("open: missing");
    }

    [Test]
    public void AssertMatches_WhenAPropertyIsExtra_ShouldFail()
    {
        Fails("""
            { "id": "x", "count": 9, "open": true, "lastChange": { "actor": "Alex", "at": "now" }, "rows": [] }
            """).ShouldContain("lastChange.at: not in the example");
    }

    [Test]
    public void AssertMatches_WhenAValueHasTheWrongKind_ShouldFail()
    {
        Fails("""
            { "id": "x", "count": "9", "open": true, "lastChange": { "actor": "Alex" }, "rows": [] }
            """).ShouldContain("count: expected number, got string");
    }

    [Test]
    public void AssertMatches_WhenNullWhereNotAllowed_ShouldFail()
    {
        Fails("""
            { "id": null, "count": 9, "open": true, "lastChange": { "actor": "Alex" }, "rows": [] }
            """, "lastChange").ShouldContain("id: null, but null is not allowed here");
    }

    [Test]
    public void AssertMatches_WhenALaterArrayElementIsWrong_ShouldFail()
    {
        Fails("""
            { "id": "x", "count": 9, "open": true, "lastChange": { "actor": "Alex" }, "rows": [{ "title": "A", "subtitle": "B" }, { "title": "C" }] }
            """).ShouldContain("rows[].subtitle: missing");
    }

    [Test]
    public void AssertMatches_WhenOpaquePathContentsDiffer_ShouldPass()
    {
        Should.NotThrow(() => Check("""
            { "id": "x", "count": 9, "open": true, "lastChange": { "anything": [1, 2], "else": null }, "rows": [] }
            """, new JsonShapeOptions { OpaquePaths = ["lastChange"] }));
    }

    [Test]
    public void AssertMatches_WhenOpaqueArrayElementsDifferFromEachOther_ShouldPass()
    {
        Should.NotThrow(() => Check("""
            { "id": "x", "count": 9, "open": true, "lastChange": { "actor": "A" }, "rows": [{ "title": "A", "subtitle": "B" }, { "other": 1 }] }
            """, new JsonShapeOptions { OpaquePaths = ["rows[]"] }));
    }

    [Test]
    public void AssertMatches_WhenOpaquePathHasTheWrongKind_ShouldFail()
    {
        Fails("""
            { "id": "x", "count": 9, "open": true, "lastChange": ["not", "an", "object"], "rows": [] }
            """, new JsonShapeOptions { OpaquePaths = ["lastChange"] }).ShouldContain("lastChange: expected object, got array");
    }

    [Test]
    public void AssertMatches_WhenOpaquePathIsNullAndNotNullable_ShouldFail()
    {
        Fails("""
            { "id": "x", "count": 9, "open": true, "lastChange": null, "rows": [] }
            """, new JsonShapeOptions { OpaquePaths = ["lastChange"] }).ShouldContain("lastChange: null, but null is not allowed here");
    }

    [Test]
    public void AssertMatches_WhenOutsideTheOpaquePath_ShouldStillCompare()
    {
        Fails("""
            { "id": "x", "count": 9, "open": true, "lastChange": { "whatever": 1 }, "rows": [{ "title": "A" }] }
            """, new JsonShapeOptions { OpaquePaths = ["lastChange"] }).ShouldContain("rows[].subtitle: missing");
    }

    // Each screen example must match itself, or no shape test that uses it can pass. Slots vary by layout, so they are
    // opaque wherever slides appear.
    [TestCaseSource(nameof(ScreenExamples))]
    public void AssertMatches_WhenScreenExampleCheckedAgainstItself_ShouldPass(string file)
    {
        using var example = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));

        Should.NotThrow(() => JsonShape.AssertMatches(example.RootElement, file, new JsonShapeOptions { OpaquePaths = ["slides[].slots"] }));
    }

    private static IEnumerable<string> ScreenExamples() =>
        Directory.GetFiles(JsonShape.ScreensDirectory(), "*.json").Select(file => Path.GetFileName(file)).Order();

    private static void Check(string actual, params string[] nullablePaths) =>
        Check(actual, new JsonShapeOptions { NullablePaths = nullablePaths });

    private static void Check(string actual, JsonShapeOptions options)
    {
        using var actualDocument = JsonDocument.Parse(actual);
        using var exampleDocument = JsonDocument.Parse(EXAMPLE);
        JsonShape.AssertMatches(actualDocument.RootElement, exampleDocument.RootElement, "the example", options);
    }

    private static string Fails(string actual, params string[] nullablePaths) =>
        Fails(actual, new JsonShapeOptions { NullablePaths = nullablePaths });

    private static string Fails(string actual, JsonShapeOptions options) =>
        Should.Throw<ShouldAssertException>(() => Check(actual, options)).Message;
}
