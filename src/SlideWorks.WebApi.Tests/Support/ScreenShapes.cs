namespace SlideWorks.WebApi.Tests.Support;

// The K4 nullable and opaque paths of each screen in src/contracts/screens, for JsonShape.AssertMatches on the screen
// records and on the real responses that carry them. JsonShape checks names and value kinds only: a test of a real
// response still asserts its wire values itself (root-relative playUrl and link, absolute share link and mcpUrl, and
// lowercase enums such as "link" and "agent").
public static class ScreenShapes
{
    // What a template may leave out of a look (T7): a built-in has no logo pair and no versions, and an art has a generator
    // or a file, never both. Declared first: the options below are built from it.
    public static readonly IReadOnlyList<string> LookNullablePaths =
    [
        "look.logos.light", "look.logos.dark", "look.art.generator", "look.art.url", "look.templateVersion", "look.latestVersion",
    ];

    // What a built-in deck's look leaves null in a sample: the six above and its template's title art, which no built-in
    // has. The art above is derived from it, so a sample with a title art beside art "none" is one the server cannot send.
    public static readonly IReadOnlyList<string> BuiltInLookNullablePaths = ["look.template.titleArt", .. LookNullablePaths];

    public static readonly JsonShapeOptions DecksLibrary = new()
    {
        NullablePaths = ["decks[].subtitle", "decks[].lastChange", "sharedWithMe[].subtitle", "sharedWithMe[].lastChange"],
    };

    public static readonly JsonShapeOptions DeckPage = new()
    {
        NullablePaths =
        [
            "author", "parts[].caption", "parts[].mark", "slides[].part", "slides[].headline", "slides[].notes", "activity[].tool",
            // SW-886 K10: the deck page's own findings never name a language.
            "findings[].language",
            "look.template.titleArt", "look.logo", "look.chromeOverrides.tracker", "look.chromeOverrides.footer", "look.chromeOverrides.progress",
            .. LookNullablePaths,
        ],
        OpaquePaths = ["slides[].slots"],
    };

    // The play view (deck-play.json), also returned by the render API.
    public static readonly JsonShapeOptions DeckPlay = new()
    {
        NullablePaths =
        [
            "author", "parts[].caption", "parts[].mark", "slides[].part", "look.template.titleArt", "look.logo",
            .. LookNullablePaths,
        ],
        OpaquePaths = ["slides[].slots"],
    };

    // The print view (deck-print.json): the play view under "deck", and the results of the last talk.
    public static readonly JsonShapeOptions DeckPrint = new()
    {
        NullablePaths =
        [
            "deck.author", "deck.parts[].caption", "deck.parts[].mark", "deck.slides[].part", "deck.look.template.titleArt", "deck.look.logo",
            .. LookNullablePaths.Select(path => "deck." + path),
            "polls[].max", "polls[].correct", "qa",
        ],
        OpaquePaths = ["deck.slides[].slots"],
    };

    public static readonly JsonShapeOptions Looks = new() { NullablePaths = ["templates[].titleArt", "templates[].lockedPalette", "templates[].version", "templates[].owner"] };

    public static readonly JsonShapeOptions Card = new()
    {
        NullablePaths =
        [
            "kicker", "title", "subtitle", "author", "look.template.titleArt", "look.logo",
            .. LookNullablePaths,
        ],
    };

    public static readonly JsonShapeOptions Sharing = new() { NullablePaths = ["lastChange", "owner.avatar.pictureUrl", "people[].name", "people[].avatar", "people[].avatar.pictureUrl"] };

    public static readonly JsonShapeOptions Profile = new() { NullablePaths = ["identity.avatar.pictureUrl", "identity.colour", "account.timeZone", "agents[].lastUsedLabel"] };

    public static readonly JsonShapeOptions Me = new() { NullablePaths = ["pictureUrl", "avatar.pictureUrl", "timeZone"] };

    public static readonly JsonShapeOptions AdminUsers = new() { NullablePaths = ["users[].pictureUrl", "users[].avatar.pictureUrl", "users[].changedLabel", "users[].changedBy"] };

    // SW-4's presenter view (presenter.json, P3): the play view's paths plus each slide's notes and headline, and the
    // deck's talk length.
    public static readonly JsonShapeOptions Presenter = new()
    {
        NullablePaths =
        [
            "author", "parts[].caption", "parts[].mark", "slides[].part", "slides[].notes", "slides[].headline", "talkMinutes", "look.template.titleArt", "look.logo",
            .. LookNullablePaths,
        ],
        OpaquePaths = ["slides[].slots"],
    };

    // The phone's pick page (presentations.json, P4).
    public static readonly JsonShapeOptions Presentations = new();

    // The presentation state (src/contracts/presentation-state.json, P1), one level above the screens: the
    // "presentationState" hub message and JoinPresentation's state. Check it with
    // JsonShape.AssertMatches(actual, ScreenShapes.PresentationStateFile, ScreenShapes.PresentationState).
    public static string PresentationStateFile => Path.Combine(JsonShape.ScreensDirectory(), "..", "presentation-state.json");

    public static readonly JsonShapeOptions PresentationState = new() { NullablePaths = ["talkMinutes"] };

    // SW-5's guest view (guest.json, J3). The file holds three samples, "live", "lobby" and "ended"; check a real response
    // against one sample with JsonShape.AssertMatches(actual, sample, "guest.json", ScreenShapes.Guest). The lobby and ended
    // samples are null where a live guest has a slide, so they are checked as the same shape with these paths nullable.
    public static readonly JsonShapeOptions Guest = new()
    {
        NullablePaths =
        [
            "deckUrl", "state.current", "state.activity", "state.room", "state.room.round", "look.logo", "look.template.titleArt",
            .. LookNullablePaths,
            // SW-883 K5: null while translations are off.
            "state.language", "translations",
        ],
        OpaquePaths = ["state.current.slots"],
    };

    // The same paths for the whole file, which JsonShape checks against itself. Each sample's look is a built-in's.
    public static readonly JsonShapeOptions GuestFile = new()
    {
        NullablePaths =
        [
            "live.state.activity", "lobby.deckUrl", "lobby.state.current", "lobby.state.activity", "lobby.state.room",
            "ended.state.current", "ended.state.activity", "ended.state.room",
            .. BuiltInLookNullablePathsUnder("live."), .. BuiltInLookNullablePathsUnder("lobby."), .. BuiltInLookNullablePathsUnder("ended."),
            // SW-883 K5: the lobby and ended samples show translations off.
            "lobby.state.language", "lobby.translations", "ended.state.language", "ended.translations",
        ],
        OpaquePaths = ["live.state.current.slots"],
    };

    // The live view (live.json, J2): GET, POST and DELETE-less reads of /api/decks/{id}/live. sessionId is null when live is false.
    // readingIn is null while translations are off or the deck is not live (SW-886 K11).
    public static readonly JsonShapeOptions Live = new() { NullablePaths = ["sessionId", "readingIn"] };

    // The audience events (audience-events.json, J4): one sample of each, as properties named by the event type. The state
    // sample is a live one, so a lobby or ended state is checked with current and activity nullable.
    public static readonly JsonShapeOptions AudienceEvents = new()
    {
        NullablePaths =
        [
            "audienceState.activity", "audienceState.room", "audienceState.room.round", "pollTally.counts", "pollControl.control.round",
            "pollControl.control.settings.max", "pollControl.control.settings.correct", "questionControl.control.round",
            "questionControl.control.here", "questionControl.control.onScreen", "questionControl.control.questions[].name",
            // SW-883 K5: null while translations are off.
            "audienceState.language",
            // SW-886 K11: null when translations were turned off.
            "readingIn.counts",
        ],
        OpaquePaths = ["audienceState.current.slots"],
    };

    // One event's own paths, for a frame checked against a single sample: AudienceState or an event with no nulls. Room is
    // null outside live (SW-9 Q3); its round is null with questions off.
    public static readonly JsonShapeOptions AudienceState = new()
    {
        NullablePaths = ["current", "activity", "room", "room.round", "language"],
        OpaquePaths = ["current.slots"],
    };

    // SW-6's poll shapes (V4, V5, V9). Each file holds named samples; the one with no nulls is the one to check a real value
    // against: poll-activity.json "answerShown", guest-poll.json "answered", poll-control.json "closed". A sample that is
    // null at a path can't tell JsonShape the kind, so a real value with a non-null there fails against it.

    // A PollActivity (poll-activity.json, V5), and audienceState.activity for a poll. Round is null in draft, Max unless
    // pick is upTo, Correct until the answer is shown.
    public static readonly JsonShapeOptions PollActivity = new() { NullablePaths = ["round", "max", "correct"] };

    // GET /api/join/{code}/poll and the 409's "current" (guest-poll.json, V5).
    public static readonly JsonShapeOptions GuestPoll = new()
    {
        NullablePaths = ["activity", "mine", "tally", "activity.round", "activity.max", "activity.correct", "tally.counts"],
    };

    // A pollTally event on its own (audience-events.json "pollTally"): counts are null while hidden from the receiver.
    public static readonly JsonShapeOptions PollTally = new() { NullablePaths = ["counts"] };

    // A PollControlView (poll-control.json, V4): every poll route's response and the pollControl event's "control".
    public static readonly JsonShapeOptions PollControl = new() { NullablePaths = ["round", "settings.max", "settings.correct"] };

    // The answer route's bodies (poll-answer.json, V5): the request, the 200, the 409 and the error bodies.
    public static readonly JsonShapeOptions PollAnswer = new()
    {
        NullablePaths =
        [
            "stale.current.activity", "stale.current.mine", "stale.current.tally", "stale.current.activity.round",
            "stale.current.activity.max", "stale.current.activity.correct", "stale.current.tally.counts",
        ],
    };

    // get_poll_results (poll-results.json, V9).
    public static readonly JsonShapeOptions PollResults = new();

    private static readonly JsonShapeOptions _pollActivityFile = new()
    {
        NullablePaths = ["draft.round", "draft.max", "draft.correct", "open.correct", "closed.max", "closed.correct"],
    };

    private static readonly JsonShapeOptions _guestPollFile = new()
    {
        NullablePaths =
        [
            "hidden.activity.max", "hidden.activity.correct", "hidden.tally.counts",
            "notAnswered.activity.correct", "notAnswered.mine", "notAnswered.tally.counts",
            "noPoll.activity", "noPoll.mine", "noPoll.tally",
            "notOpen.activity.round", "notOpen.activity.max", "notOpen.activity.correct", "notOpen.mine", "notOpen.tally",
            "joinedLate.activity.max", "joinedLate.activity.correct", "joinedLate.mine",
        ],
    };

    private static readonly JsonShapeOptions _pollControlFile = new()
    {
        NullablePaths = ["draft.round", "draft.settings.max", "draft.settings.correct", "open.settings.max", "open.settings.correct"],
    };

    // SW-9's Q&A shapes (Q1, Q4 to Q7, Q10). As with the poll files, check a real value against the sample with no nulls:
    // qa-room.json "open", guest-questions.json "view", qa-control.json "live". A question's name is null when anonymous.

    // A QaRoom (qa-room.json, Q1): audienceState.room while live, and GuestQuestionsView.room. Round is null with questions off.
    public static readonly JsonShapeOptions QaRoom = new() { NullablePaths = ["round"] };

    // GET /api/join/{code}/questions (guest-questions.json "view", Q4), and the stale vote's "current".
    public static readonly JsonShapeOptions GuestQuestions = new() { NullablePaths = ["room.round", "questions[].name"] };

    // The ask route's bodies (question-ask.json, Q4): the request (name null for anonymous), the 201, and the error bodies.
    public static readonly JsonShapeOptions QuestionAsk = new() { NullablePaths = ["request.name", "created.question.name"] };

    // The votes route's bodies (question-votes.json, Q4): the request, the 200, the 409 and the error bodies.
    public static readonly JsonShapeOptions QuestionVotes = new() { NullablePaths = ["stale.current.room.round", "stale.current.questions[].name"] };

    // A QaControlView (qa-control.json "live", Q6): every Q&A presenter route's response and the questionControl event's
    // "control". Round, here and onScreen are null when not live.
    public static readonly JsonShapeOptions QaControl = new() { NullablePaths = ["round", "here", "onScreen", "questions[].name"] };

    // deck-checks.json (SW-3 K2): the error is null in the sample, so error strings are asserted directly, not through JsonShape.
    // A finding's language is null while translations are off (SW-886 K10).
    public static readonly JsonShapeOptions DeckChecks = new() { NullablePaths = ["error", "findings[].language"] };

    // get_questions (questions-report.json, Q10).
    public static readonly JsonShapeOptions QuestionsReport = new() { NullablePaths = ["sessions[].questions[].name"] };

    // A questionTally event on its own (audience-events.json, Q5): no nulls.
    public static readonly JsonShapeOptions QuestionTally = new();

    // A reactions event on its own (audience-events.json, Q7): no nulls.
    public static readonly JsonShapeOptions ReactionsBurst = new();

    // A wordCloud or openText event on its own (audience-events.json, SW-8): no nulls; round is left out in draft.
    public static readonly JsonShapeOptions OpenResponseEvent = new();

    // A guest's word-cloud or open-text view, a submit body or its stored reply (word-cloud.json, open-text.json): no nulls.
    public static readonly JsonShapeOptions GuestResponse = new();

    // The presenter's open-response control view (open-response-control.json): no nulls.
    public static readonly JsonShapeOptions OpenResponseControl = new();

    private static readonly JsonShapeOptions _qaRoomFile = new() { NullablePaths = ["off.round"] };

    private static readonly JsonShapeOptions _guestQuestionsFile = new()
    {
        NullablePaths = ["view.room.round", "view.questions[].name", "ownHidden.room.round", "ownHidden.questions[].name"],
    };

    private static readonly JsonShapeOptions _qaControlFile = new()
    {
        NullablePaths = ["live.questions[].name", "notLive.round", "notLive.here", "notLive.onScreen"],
    };

    // A sample that carries a built-in deck's look, checked against itself: only what a built-in leaves null in a look
    // may be null (T7), under the look's place in the file.
    private static JsonShapeOptions BuiltInLookFile(string under = "", params string[] extra) => new()
    {
        NullablePaths = [.. BuiltInLookNullablePathsUnder(under), .. extra],
        OpaquePaths = ["slides[].slots"],
    };

    private static IReadOnlyList<string> BuiltInLookNullablePathsUnder(string prefix) => BuiltInLookNullablePaths.Select(path => prefix + path).ToList();

    // The nullable and opaque paths for each screen file when JsonShapeTests checks a sample against itself. A file with
    // no entry is checked with slides[].slots opaque, which is every play-shaped screen. The six deck samples show a
    // built-in deck as the server sends it, so their looks hold a built-in's nulls.
    public static JsonShapeOptions SelfCheckOf(string file) => file switch
    {
        "deck-play.json" or "presenter.json" => BuiltInLookFile("", "parts[].caption", "parts[].mark"),
        "deck-page.json" => BuiltInLookFile("", "parts[].caption", "parts[].mark", "findings[].language"),
        "card.json" => BuiltInLookFile(),
        "deck-print.json" => DeckPrint,
        "deck-checks.json" => DeckChecks,
        "look-change.json" => BuiltInLookFile("response."),
        "guest.json" => GuestFile,
        "audience-events.json" => AudienceEvents,
        "poll-activity.json" => _pollActivityFile,
        "guest-poll.json" => _guestPollFile,
        "poll-control.json" => _pollControlFile,
        "poll-answer.json" => PollAnswer,
        "poll-results.json" => PollResults,
        "qa-room.json" => _qaRoomFile,
        "guest-questions.json" => _guestQuestionsFile,
        "question-ask.json" => QuestionAsk,
        "question-votes.json" => QuestionVotes,
        "qa-control.json" => _qaControlFile,
        "questions-report.json" => QuestionsReport,
        "sharing.json" => Sharing,
        "profile.json" => Profile,
        TemplateScreenShapes.TEMPLATES_FILE => TemplateScreenShapes.Templates,
        TemplateScreenShapes.EDITOR_FILE => TemplateScreenShapes.Editor,
        TemplateScreenShapes.LAYOUT_REVIEW_FILE => TemplateScreenShapes.LayoutReview,
        TemplateScreenShapes.PUBLIC_FILE => TemplateScreenShapes.Public,
        _ => new JsonShapeOptions { OpaquePaths = ["slides[].slots"] },
    };
}
