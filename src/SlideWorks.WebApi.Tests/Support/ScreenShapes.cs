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

    // SW-4's presenter view (presenter.json, P3): the play view's paths plus each slide's notes and headline, and the
    // deck's talk length.
    public static readonly JsonShapeOptions Presenter = new()
    {
        NullablePaths = ["author", "slides[].part", "slides[].notes", "slides[].headline", "talkMinutes", "look.template.titleArt", "look.logo"],
        OpaquePaths = ["slides[].slots"],
    };

    // The phone's pick page (presentations.json, P4).
    public static readonly JsonShapeOptions Presentations = new();

    // The presentation state (src/contracts/presentation-state.json, P1), one level above the screens: the
    // "presentationState" hub message and JoinPresentation's state. Check it with
    // JsonShape.AssertMatches(actual, ScreenShapes.PresentationStateFile, ScreenShapes.PresentationState).
    public static string PresentationStateFile => Path.Combine(JsonShape.ScreensDirectory(), "..", "presentation-state.json");

    public static readonly JsonShapeOptions PresentationState = new() { NullablePaths = ["talkMinutes"] };
}
