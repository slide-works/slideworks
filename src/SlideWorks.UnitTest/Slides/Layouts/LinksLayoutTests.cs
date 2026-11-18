using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class LinksLayoutTests
{
    private const string OK_URL = "https://example.com/a";

    [Test]
    public void Validate_WhenALinkIsJavascript_ShouldRefuse()
    {
        Validate(Slide(Link("javascript:alert(1)"))).Errors.ShouldHaveSingleItem().ShouldStartWith("links.links[0].url: ");
    }

    [Test]
    public void Validate_WhenALinkIsData_ShouldRefuse()
    {
        Validate(Slide(Link("data:text/html,<b>x</b>"))).Errors.ShouldHaveSingleItem().ShouldStartWith("links.links[0].url: ");
    }

    [Test]
    public void Validate_WhenALinkHasUserInfo_ShouldRefuse()
    {
        Validate(Slide(Link("https://user:secret@example.com/a"))).Errors.ShouldHaveSingleItem().ShouldContain("user name or password");
    }

    [Test]
    public void Validate_WhenTheQrUrlIsJavascript_ShouldRefuse()
    {
        Validate(Slide(Link(OK_URL), qr: """{ "url": "javascript:alert(1)" }""")).Errors
            .ShouldHaveSingleItem().ShouldStartWith("links.qr.url: ");
    }

    [Test]
    public void Validate_WhenTheQrUrlIs201_ShouldRefuse()
    {
        var url = "https://example.com/" + new string('a', 181);

        Validate(Slide(Link(OK_URL), qr: $$"""{ "url": "{{url}}" }""")).Errors
            .ShouldHaveSingleItem().ShouldContain("at most 200 characters (got 201)");
    }

    [Test]
    public void Validate_WhenTheQrUrlIs200_ShouldAccept()
    {
        var url = "https://example.com/" + new string('a', 180);

        Validate(Slide(Link(OK_URL), qr: $$"""{ "url": "{{url}}" }""")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenSevenLinks_ShouldRefuse()
    {
        var links = Enumerable.Repeat(Link(OK_URL, "Short"), 7);

        Validate(Slide(links.ToArray())).Errors.ShouldHaveSingleItem().ShouldStartWith("links.links: ");
    }

    [Test]
    public void Validate_WhenWrappedRowsPassTheBody_ShouldRefuse()
    {
        var title = string.Join(' ', Enumerable.Repeat("Small batches", 10))[..80];
        var links = Enumerable.Repeat(Link(OK_URL, title), 6).ToArray();

        Validate(Slide(links, qr: """{ "url": "https://example.com/q" }""")).Errors
            .ShouldHaveSingleItem().ShouldStartWith("links.links: too much text to fit");
    }

    [Test]
    public void Validate_WhenSixRowsSitBesideAQrCode_ShouldAccept()
    {
        var links = Enumerable.Repeat(Link(OK_URL, "A short title", "A one line source"), 6).ToArray();

        Validate(Slide(links, qr: """{ "url": "https://example.com/q", "caption": "example.com/q" }""")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheTitleWrapsOnlyBesideTheQrCode_ShouldRefuseOnlyThere()
    {
        var title = string.Join(' ', Enumerable.Repeat("Small batches", 10))[..80];
        var links = Enumerable.Repeat(Link(OK_URL, title), 6).ToArray();

        Validate(Slide(links)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        var slots = JsonNode.Parse(Slide(Link(OK_URL)))!.AsObject();
        slots["emphasis"] = JsonNode.Parse("""[{ "text": "missing phrase", "tone": "accent" }]""");

        Validate(slots.ToJsonString()).Errors.ShouldHaveSingleItem().ShouldContain("missing phrase");
    }

    [Test]
    public void Validate_WhenTheLayoutIsOnADarkSurface_ShouldRefuse()
    {
        using var document = JsonDocument.Parse(Slide(Link(OK_URL)));

        SlideValidator.Validate(LinksLayout.Definition, document.RootElement, FitWidths.Default, "dark").Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Count_WhenRead_ShouldBeZero()
    {
        using var document = JsonDocument.Parse(Slide(Link(OK_URL, "Title", "Source")));

        SlideSteps.Count(LinksLayout.Definition, document.RootElement).ShouldBe(0);
    }

    private static string Link(string url, string title = "A title", string? source = null) =>
        source is null
            ? $$"""{ "title": "{{title}}", "url": "{{url}}" }"""
            : $$"""{ "kind": "Talk", "title": "{{title}}", "source": "{{source}}", "url": "{{url}}" }""";

    private static string Slide(string link, string? qr = null) => Slide([link], qr);

    private static string Slide(string[] links, string? qr = null) =>
        $$"""{ "headline": "Where to read more", "links": [{{string.Join(',', links)}}]{{(qr is null ? "" : $$""", "qr": {{qr}}""")}} }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(LinksLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }
}
