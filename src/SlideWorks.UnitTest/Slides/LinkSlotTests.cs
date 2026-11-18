using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// The link slot is a security boundary: its text becomes an href. Every case of the shared case file runs
// here, and the client's check runs the same file.
[TestFixture]
public class LinkSlotTests
{
    private const string PATH = "links.links[0].url";

    [Test]
    public void Validate_WhenRunOverTheContractCases_ShouldAgreeWithEachOne()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("link-cases.json")));
        var cases = file.RootElement.GetProperty("cases").EnumerateArray().ToList();

        cases.Count.ShouldBeGreaterThanOrEqualTo(60);
        foreach (var linkCase in cases)
        {
            var value = linkCase.GetProperty("value");
            var errors = Errors(value);

            (errors.Count == 0).ShouldBe(linkCase.GetProperty("ok").GetBoolean(), value.GetRawText());
            errors.Count.ShouldBeLessThanOrEqualTo(1, value.GetRawText());
        }
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("JaVaScRiPt:alert(1)")]
    [TestCase("javascript://example.org/%0Aalert(1)")]
    [TestCase("data:text/html,x")]
    [TestCase("vbscript:x")]
    [TestCase("file:///etc/hosts")]
    [TestCase("ftp://example.org")]
    [TestCase("blob:https://example.org/a")]
    [TestCase("mailto:a@example.org")]
    [TestCase("//example.org")]
    [TestCase("/decks/a")]
    [TestCase("example.org/a")]
    [TestCase("https:example.org")]
    [TestCase("https:/example.org")]
    [TestCase("https:///example.org")]
    [TestCase("https://")]
    [TestCase("https://:8443/a")]
    [TestCase("https://?x=1")]
    [TestCase("https://#part")]
    [TestCase("")]
    public void Validate_WhenTheSchemeIsNotHttp_ShouldRefuse(string value)
    {
        Errors(value).ShouldBe([PATH + ": must be an http or https address, such as https://example.com/page"]);
    }

    [TestCase("https://user:pw@example.org/")]
    [TestCase("https://user@example.org/")]
    [TestCase("https://example.org@other.example/")]
    [TestCase("https://example.org:443@other.example")]
    [TestCase("https://a%40b@example.org/")]
    public void Validate_WhenItHoldsUserInfo_ShouldRefuse(string value)
    {
        Errors(value).ShouldBe([PATH + ": must not hold a user name or password"]);
    }

    // Rule 6. With a user-info @ as well (https://example.org%2f@evil.example/) the @ is reported; the case file holds that one.
    [TestCase("https://exa%6dple.org/")]
    [TestCase("https://[fe80::1%eth0]/")]
    [TestCase("https://[fe80::1%25eth0]/")]
    [TestCase("https://example.org:80%31/")]
    public void Validate_WhenTheAuthorityHoldsAPercentSign_ShouldRefuse(string value)
    {
        Errors(value).ShouldBe([PATH + ": must not hold a percent sign before the path; write the host and port as they are"]);
    }

    // Rule 7. What sits before the path is a host and at most a port: a quote or a bracket there could end the href.
    [TestCase("http://example.org\"onclick=\"alert(1)")]
    [TestCase("https://example.org'x/")]
    [TestCase("https://exa<mple.org/")]
    [TestCase("https://example.org(1)/")]
    [TestCase("https://exam_ple.org/")]
    [TestCase("https://example.org;x/")]
    [TestCase("https://.example.org/")]
    [TestCase("https://example.org-/")]
    [TestCase("https://example.org:port/")]
    [TestCase("https://example.org:123456/")]
    [TestCase("https://[::1/")]
    [TestCase("https://[::g]/")]
    public void Validate_WhenTheAuthorityHoldsPunctuation_ShouldRefuse(string value)
    {
        Errors(value).ShouldBe([PATH + ": must hold only letters, digits, dots and hyphens in the host, then at most a colon and a port number"]);
    }

    [TestCase("https://@example.org/")]
    [TestCase("https://:pw@example.org/")]
    [TestCase("https://example.org:99999/")]
    [TestCase("https://example.org:port/")]
    [TestCase("https://[::1/")]
    public void Validate_WhenTheHostDoesNotParse_ShouldRefuse(string value)
    {
        Errors(value).Count.ShouldBe(1);
    }

    [TestCase("https://example.org/a?next=user@example.org")]
    [TestCase("https://example.org/@handle")]
    [TestCase("https://example.org#a@b")]
    [TestCase("https://example.org/a:b")]
    public void Validate_WhenAnAtSignComesAfterTheHost_ShouldAccept(string value)
    {
        Errors(value).ShouldBeEmpty();
    }

    // One test with a loop, not test cases: the values hold control characters, which do not belong in a test's name.
    [Test]
    public void Validate_WhenACharacterIsNotPrintableAscii_ShouldRefuse()
    {
        string[] values =
        [
            " https://example.org",
            "https://example.org ",
            "\thttps://example.org",
            "java\tscript:alert(1)",
            "java\nscript:alert(1)",
            "ht\ntps://example.org",
            "https://example.org\r",
            "https://example.org\0",
            "\0javascript:alert(1)",
            "https://example.org/a\u007f",
            "https://example.org\u0085",
            "\ufeffhttps://example.org",
            "https://example.org/\u00a0a",
            "https://example.org/\u2028a",
            "https://ex\u00e4mple.org",
            "https://example.org/\ud83d\ude00",
            "https://exa mple.org",
        ];

        foreach (var value in values)
        {
            Errors(value).ShouldBe(
                [PATH + ": must not hold spaces, control characters or characters outside ASCII; write an international host in punycode"],
                JsonSerializer.Serialize(value));
        }
    }

    [TestCase("https:\\\\example.org")]
    [TestCase("https://example.org\\@other.example/")]
    [TestCase("https://example.org/a\\b")]
    [TestCase("https:/\\example.org")]
    public void Validate_WhenItHoldsABackslash_ShouldRefuse(string value)
    {
        Errors(value).ShouldBe([PATH + ": must not hold a backslash"]);
    }

    [Test]
    public void Validate_WhenItIsLongerThanTheSlotsLimit_ShouldRefuse()
    {
        var atLimit = "https://example.org/" + new string('a', 180);
        var slot = new LinkSlot(200);

        Errors(atLimit, slot).ShouldBeEmpty();
        Errors(atLimit + "a", slot).ShouldBe([PATH + ": at most 200 characters (got 201)"]);
        Errors(atLimit + "a").ShouldBeEmpty();
        Errors("https://example.org/" + new string('a', 481)).ShouldBe([PATH + ": at most 500 characters (got 501)"]);
    }

    [Test]
    public void Validate_WhenAnOverlongValueIsAlsoHostile_ShouldOnlyReportItsLength()
    {
        Errors("javascript:" + new string('\n', 100_000)).ShouldBe([PATH + ": at most 500 characters (got 100011)"]);
    }

    [TestCase("7")]
    [TestCase("null")]
    [TestCase("""{ "href": "https://example.org" }""")]
    [TestCase("""["https://example.org"]""")]
    public void Validate_WhenTheValueIsNotAString_ShouldRefuse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var validation = new SlotValidation();

        new LinkSlot().Validate(document.RootElement, PATH, validation);

        validation.Errors.ShouldBe([PATH + ": expected a link"]);
    }

    [Test]
    public void Validate_WhenAccepted_ShouldLeaveTheValueAsSent()
    {
        // Nothing is trimmed, lower-cased or re-encoded on the way in: what passes is what is stored.
        LinkSlot.Problem("HTTPS://EXAMPLE.ORG/A%20b?x=1#Part").ShouldBeNull();
        LinkSlot.Problem("https://example.org/a b").ShouldNotBeNull();
    }

    [TestCase(0)]
    [TestCase(501)]
    public void Constructor_WhenTheLimitIsOutOfRange_ShouldThrow(int max)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkSlot(max));
    }

    [Test]
    public void ToJsonSchema_WhenAsked_ShouldBeAUriStringWithTheLimit()
    {
        new LinkSlot(200).ToJsonSchema().ToJsonString().ShouldBe("""{"type":"string","format":"uri","maxLength":200}""");
        new LinkSlot().MaxLength.ShouldBe(500);
        new LinkSlot().Kind.ShouldBe("link");
    }

    private static IReadOnlyList<string> Errors(string value, LinkSlot? slot = null)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return Errors(document.RootElement, slot);
    }

    private static IReadOnlyList<string> Errors(JsonElement value, LinkSlot? slot = null)
    {
        var validation = new SlotValidation();
        (slot ?? new LinkSlot()).Validate(value, PATH, validation);
        return validation.Errors;
    }
}
