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

    private static void Check(string actual, params string[] nullablePaths)
    {
        using var actualDocument = JsonDocument.Parse(actual);
        using var exampleDocument = JsonDocument.Parse(EXAMPLE);
        JsonShape.AssertMatches(actualDocument.RootElement, exampleDocument.RootElement, "the example", nullablePaths);
    }

    private static string Fails(string actual, params string[] nullablePaths) =>
        Should.Throw<ShouldAssertException>(() => Check(actual, nullablePaths)).Message;
}
