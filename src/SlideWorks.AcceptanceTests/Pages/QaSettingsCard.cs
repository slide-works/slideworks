namespace SlideWorks.AcceptanceTests.Pages;

// The deck page's "Q&A and reactions" card (SW-9 Q9), for a person who can present the deck: the deck's five settings
// and the selected slide's switch. One member for every id in src/SlideWorks.Client/src/qa/testIds.ts under
// QA_SETTINGS_CARD_TEST_IDS; add one there and here together.
public sealed class QaSettingsCard(IPage page)
{
    public ILocator Root => page.GetByTestId("qa-settings-card");

    // "Q&A and reactions"
    public ILocator Title => Root.GetByTestId("qa-settings-card-title");

    // "Questions": Open, Closed, Off
    public ILocator Questions => Root.GetByTestId("qa-settings-card-questions");

    public ILocator QuestionsOpen => Root.GetByTestId("qa-settings-card-questions-open");

    public ILocator QuestionsClosed => Root.GetByTestId("qa-settings-card-questions-closed");

    public ILocator QuestionsOff => Root.GetByTestId("qa-settings-card-questions-off");

    // "Review questions first"
    public ILocator Review => Root.GetByTestId("qa-settings-card-review");

    // "Names allowed"
    public ILocator Names => Root.GetByTestId("qa-settings-card-names");

    // "Reactions"
    public ILocator Reactions => Root.GetByTestId("qa-settings-card-reactions");

    // "Shown questions appear": "over any slide" or "only on the Q&A slide"
    public ILocator ShowOn => Root.GetByTestId("qa-settings-card-show-on");

    public ILocator ShowOnAnySlide => Root.GetByTestId("qa-settings-card-show-on-any-slide");

    public ILocator ShowOnQaSlide => Root.GetByTestId("qa-settings-card-show-on-qa-slide");

    // "Slide 3: questions", and its reason when the deck has questions off.
    public ILocator SlideQuestions => Root.GetByTestId("qa-settings-card-slide-questions");

    public ILocator SlideQuestionsReason => Root.GetByTestId("qa-settings-card-slide-questions-reason");

    // "Slide 3: reactions", and its reason when the deck has reactions off.
    public ILocator SlideReactions => Root.GetByTestId("qa-settings-card-slide-reactions");

    public ILocator SlideReactionsReason => Root.GetByTestId("qa-settings-card-slide-reactions-reason");

    // A failed change's message; the control goes back.
    public ILocator Error => Root.GetByTestId("qa-settings-card-error");
}
