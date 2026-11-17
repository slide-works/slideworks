namespace SlideWorks.AcceptanceTests.Pages;

// The poll card on a guest's phone (SW-6 V6), inside GuestPage.Activity while a poll is on screen. One member for every
// id in src/SlideWorks.Client/src/polls/testIds.ts (POLL_CARD_TEST_IDS and the per-option ids); add one there and here
// together. Per-option members take the option's 0-based position, the same index as an answer's picks.
public sealed class GuestPollCard(IPage page)
{
    public ILocator Root => page.GetByTestId("poll-card");

    // "Poll · Pick one", "Poll · Pick up to 2"
    public ILocator Kind => page.GetByTestId("poll-card-kind");

    // "Round 2", shown from round 2 on
    public ILocator Round => page.GetByTestId("poll-card-round");

    public ILocator Question => page.GetByTestId("poll-card-question");

    // "Private: <presenter> sees totals only"
    public ILocator Privacy => page.GetByTestId("poll-card-privacy");

    // The radio group for pick one, and the checkbox group for pick several.
    public ILocator Options => page.GetByTestId("poll-options");

    public ILocator OptionsMany => page.GetByTestId("poll-options-many");

    // The foot under the options and each of its lines.
    public ILocator Foot => page.GetByTestId("poll-foot");

    public ILocator TapHint => page.GetByTestId("poll-foot-tap-hint");

    public ILocator PickCount => page.GetByTestId("poll-foot-pick-count");

    public ILocator Send => page.GetByTestId("poll-foot-send");

    public ILocator Sending => page.GetByTestId("poll-foot-sending");

    public ILocator Sent => page.GetByTestId("poll-foot-sent");

    // "38 answers so far"
    public ILocator Answered => page.GetByTestId("poll-foot-answered");

    public ILocator ResultsLater => page.GetByTestId("poll-foot-results-later");

    public ILocator ChangeHint => page.GetByTestId("poll-foot-change-hint");

    public ILocator Change => page.GetByTestId("poll-foot-change");

    public ILocator Closed => page.GetByTestId("poll-foot-closed");

    public ILocator NoAnswer => page.GetByTestId("poll-foot-no-answer");

    public ILocator JoinedLate => page.GetByTestId("poll-foot-joined-late");

    public ILocator NotOpen => page.GetByTestId("poll-foot-not-open");

    // A failed call's message.
    public ILocator Failed => page.GetByTestId("poll-foot-failed");

    // After the presenter shows the answer.
    public ILocator Verdict => page.GetByTestId("poll-verdict");

    public ILocator VerdictRight => page.GetByTestId("poll-verdict-right");

    public ILocator VerdictMiss => page.GetByTestId("poll-verdict-miss");

    // A 409 came back: the message, Send it and Clear.
    public ILocator Conflict => page.GetByTestId("poll-conflict");

    public ILocator ConflictMessage => page.GetByTestId("poll-conflict-message");

    public ILocator ConflictSend => page.GetByTestId("poll-conflict-send");

    public ILocator ConflictClear => page.GetByTestId("poll-conflict-clear");

    // Offline or reconnecting with an unsent pick.
    public ILocator Queued => page.GetByTestId("poll-queued");

    public ILocator QueuedMessage => page.GetByTestId("poll-queued-message");

    // One option's radio or checkbox, and its bar, percentage, "You" mark and correct mark, each present only when shown.
    public ILocator Option(int position) => page.GetByTestId($"poll-option-{position}");

    public ILocator OptionBar(int position) => page.GetByTestId($"poll-option-bar-{position}");

    public ILocator OptionPercent(int position) => page.GetByTestId($"poll-option-percent-{position}");

    public ILocator OptionYou(int position) => page.GetByTestId($"poll-option-you-{position}");

    public ILocator OptionCorrect(int position) => page.GetByTestId($"poll-option-correct-{position}");
}
