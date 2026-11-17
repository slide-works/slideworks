using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Tests.Rendering;

// The share card's cache key and ETag (L9): "<deckId>:<revision>:<lookHash>:<sharing>". Anything that changes the card
// changes the key, and nothing else does.
[TestFixture]
public class RenderKeyTests
{
    private const string DECK = "dk_ABC123";
    private const string LOOK = """{"template":{"name":"briefing"},"palette":{"name":"cobalt"}}""";

    [Test]
    public void For_ShouldJoinDeckRevisionLookHashAndSharing()
    {
        var parts = RenderKey.For(DECK, 7, LOOK, "link").Split(':');

        parts.Length.ShouldBe(4);
        parts[0].ShouldBe(DECK);
        parts[1].ShouldBe("7");
        parts[2].ShouldMatch("^[0-9a-f]{16}$");
        parts[3].ShouldBe("link");
    }

    [Test]
    public void For_WhenSameInputs_ShouldBeTheSameKey()
    {
        RenderKey.For(DECK, 7, LOOK, "link").ShouldBe(RenderKey.For(DECK, 7, LOOK, "link"));
    }

    [Test]
    public void For_WhenRevisionChanges_ShouldChangeTheKey()
    {
        RenderKey.For(DECK, 8, LOOK, "link").ShouldNotBe(RenderKey.For(DECK, 7, LOOK, "link"));
    }

    [Test]
    public void For_WhenLookChanges_ShouldChangeTheKey()
    {
        RenderKey.For(DECK, 7, LOOK.Replace("cobalt", "ember"), "link").ShouldNotBe(RenderKey.For(DECK, 7, LOOK, "link"));
    }

    [Test]
    public void For_WhenSharingChanges_ShouldChangeTheKey()
    {
        RenderKey.For(DECK, 7, LOOK, "private").ShouldNotBe(RenderKey.For(DECK, 7, LOOK, "link"));
    }

    [Test]
    public void For_WhenDeckChanges_ShouldChangeTheKey()
    {
        RenderKey.For("dk_OTHER1", 7, LOOK, "link").ShouldNotBe(RenderKey.For(DECK, 7, LOOK, "link"));
    }

    [Test]
    public void Generic_ShouldBeGeneric()
    {
        RenderKey.GENERIC.ShouldBe("generic");
    }
}
