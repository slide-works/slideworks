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
        NullablePaths = ["slides[].part", "slides[].headline", "slides[].notes", "activity[].tool"],
        OpaquePaths = ["slides[].slots"],
    };

    public static readonly JsonShapeOptions Sharing = new() { NullablePaths = ["lastChange"] };

    public static readonly JsonShapeOptions Me = new() { NullablePaths = ["pictureUrl"] };

    public static readonly JsonShapeOptions AdminUsers = new() { NullablePaths = ["users[].pictureUrl", "users[].changedLabel", "users[].changedBy"] };
}
