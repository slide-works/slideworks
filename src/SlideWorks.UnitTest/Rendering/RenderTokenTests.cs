using SlideWorks.Core.Rendering;

namespace SlideWorks.UnitTest.Rendering;

[TestFixture]
public class RenderTokenTests
{
    private const string DECK = "dk_render0001";
    private const string KEY = "a-test-signing-key-of-at-least-32-characters";

    private SteppedClock _clock = null!;
    private RenderTokens _tokens = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new SteppedClock(DateTimeOffset.FromUnixTimeSeconds(1_794_819_600));
        _tokens = new RenderTokens(new RenderTokenOptions { Key = KEY }, _clock);
    }

    [TestCase(RenderPurpose.Preview, 600)]
    [TestCase(RenderPurpose.Card, 60)]
    public void Validate_WhenInsideAndAfterThePurposesLifetime_ShouldPassThenFail(RenderPurpose purpose, int seconds)
    {
        var token = _tokens.Issue(DECK, purpose);

        _clock.Advance(TimeSpan.FromSeconds(seconds) - TimeSpan.FromMilliseconds(1));
        _tokens.Validate(token, DECK, purpose).ShouldBeTrue();
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        _tokens.Validate(token, DECK, purpose).ShouldBeFalse();
    }

    [Test]
    public void LifetimeOf_ShouldBeTenMinutesForPreviewAndSixtySecondsForCard()
    {
        RenderTokens.LifetimeOf(RenderPurpose.Preview).ShouldBe(TimeSpan.FromMinutes(10));
        RenderTokens.LifetimeOf(RenderPurpose.Card).ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Test]
    public void Validate_WhenAnotherDeck_ShouldFail()
    {
        var token = _tokens.Issue(DECK, RenderPurpose.Preview);

        _tokens.Validate(token, "dk_render0002", RenderPurpose.Preview).ShouldBeFalse();
        _tokens.Validate(token, DECK + "x", RenderPurpose.Preview).ShouldBeFalse();
    }

    [TestCase(RenderPurpose.Preview, RenderPurpose.Card)]
    [TestCase(RenderPurpose.Card, RenderPurpose.Preview)]
    public void Validate_WhenAnotherPurpose_ShouldFail(RenderPurpose issued, RenderPurpose asked)
    {
        var token = _tokens.Issue(DECK, issued);

        _tokens.Validate(token, DECK, asked).ShouldBeFalse();
    }

    [Test]
    public void Validate_WhenTampered_ShouldFail()
    {
        var token = _tokens.Issue(DECK, RenderPurpose.Card);
        var dot = token.IndexOf('.', StringComparison.Ordinal);
        var expiry = long.Parse(token[..dot], System.Globalization.CultureInfo.InvariantCulture);
        var mac = token[(dot + 1)..];
        var flipped = (mac[0] == 'A' ? 'B' : 'A') + mac[1..];

        _tokens.Validate($"{expiry + 3_600_000}.{mac}", DECK, RenderPurpose.Card).ShouldBeFalse();
        _tokens.Validate($"{token[..dot]}.{flipped}", DECK, RenderPurpose.Card).ShouldBeFalse();
        _tokens.Validate(token + "A", DECK, RenderPurpose.Card).ShouldBeFalse();
        _tokens.Validate(token[..^1], DECK, RenderPurpose.Card).ShouldBeFalse();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(".")]
    [TestCase("not-a-token")]
    [TestCase("-1.AAAA")]
    [TestCase("99999999999999999999999.AAAA")]
    public void Validate_WhenMalformed_ShouldFailWithoutThrowing(string? token)
    {
        _tokens.Validate(token, DECK, RenderPurpose.Preview).ShouldBeFalse();
    }

    [Test]
    public void Validate_WhenSignedWithAnotherKey_ShouldFail()
    {
        var other = new RenderTokens(new RenderTokenOptions { Key = KEY + "-other" }, _clock);

        _tokens.Validate(other.Issue(DECK, RenderPurpose.Preview), DECK, RenderPurpose.Preview).ShouldBeFalse();
    }

    [Test]
    public void Validate_WhenReusedInsideItsLifetime_ShouldPassEachTime()
    {
        var token = _tokens.Issue(DECK, RenderPurpose.Preview);

        for (var minute = 0; minute < 10; minute++)
        {
            _tokens.Validate(token, DECK, RenderPurpose.Preview).ShouldBeTrue($"use at minute {minute}");
            _clock.Advance(TimeSpan.FromMinutes(1) - TimeSpan.FromMilliseconds(minute == 9 ? 1 : 0));
        }
    }

    [Test]
    public void Constructor_WhenKeyIsShort_ShouldRefuse()
    {
        Should.Throw<InvalidOperationException>(() => new RenderTokens(new RenderTokenOptions { Key = new string('k', 31) }, _clock))
            .Message.ShouldBe("RenderTokens:Key must be at least 32 characters");
    }
}
