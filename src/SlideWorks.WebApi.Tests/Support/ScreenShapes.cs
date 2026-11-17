namespace SlideWorks.WebApi.Tests.Support;

// The K4 nullable and opaque paths of each screen in src/contracts/screens, for JsonShape.AssertMatches on the screen
// records and on the real responses that carry them. JsonShape checks names and value kinds only: a test of a real
// response still asserts its wire values itself (root-relative playUrl and link, absolute share link and mcpUrl, and
// lowercase enums such as "link" and "agent").
public static class ScreenShapes
{
    public static readonly JsonShapeOptions DecksLibrary = new()
    {
        NullablePaths = ["decks[].subtitle", "decks[].lastChange", "sharedWithMe[].subtitle", "sharedWithMe[].lastChange"],
    };

    public static readonly JsonShapeOptions DeckPage = new()
    {
        NullablePaths =
        [
            "author", "slides[].part", "slides[].headline", "slides[].notes", "activity[].tool",
            "look.template.titleArt", "look.logo", "look.chromeOverrides.tracker", "look.chromeOverrides.footer", "look.chromeOverrides.progress",
        ],
        OpaquePaths = ["slides[].slots"],
    };

    // The play view (deck-play.json), also returned by the render API.
    public static readonly JsonShapeOptions DeckPlay = new()
    {
        NullablePaths = ["author", "slides[].part", "look.template.titleArt", "look.logo"],
        OpaquePaths = ["slides[].slots"],
    };

    public static readonly JsonShapeOptions Looks = new() { NullablePaths = ["templates[].titleArt", "templates[].lockedPalette"] };

    public static readonly JsonShapeOptions Card = new() { NullablePaths = ["title", "subtitle", "author", "look.template.titleArt", "look.logo"] };

    public static readonly JsonShapeOptions Sharing = new() { NullablePaths = ["lastChange"] };

    public static readonly JsonShapeOptions Me = new() { NullablePaths = ["pictureUrl"] };

    public static readonly JsonShapeOptions AdminUsers = new() { NullablePaths = ["users[].pictureUrl", "users[].changedLabel", "users[].changedBy"] };
}
