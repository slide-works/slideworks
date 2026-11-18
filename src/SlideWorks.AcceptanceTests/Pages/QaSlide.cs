namespace SlideWorks.AcceptanceTests.Pages;

// The big screen's Q&A parts (SW-9 Q9). The qa slide's members look inside one slide: pass the slide's locator, such as
// PlayerPage.Slide(6), since every slide of the player stays rendered. The same slide shows in the player, the audience
// window, the presenter window and the deck page's thumbnails. The question card and the reactions layer sit over every
// slide, once per player, so their members look on the slide's page. One member for every id in
// src/SlideWorks.Client/src/qa/testIds.ts under QA_SLIDE_TEST_IDS, the qa slide's per-row ids, QA_CARD_TEST_IDS and
// QA_REACTIONS_LAYER_TEST_IDS; add one there and here together. Per-row members take the question's number (1-based).
public sealed class QaSlide(ILocator slide)
{
    // A display: contents wrapper inside the layout's root (layout-qa), with no box of its own: visibility checks work,
    // but bounding boxes, screenshots, scrolling and hovering need the slide locator (layout-qa) instead.
    public ILocator Root => slide.GetByTestId("qa-slide");

    // "Q&A"
    public ILocator Kicker => slide.GetByTestId("qa-slide-kicker");

    // The slot's title, or "Questions from the room"
    public ILocator Title => slide.GetByTestId("qa-slide-title");

    // The join pill, top right, while the deck is live.
    public ILocator Join => slide.GetByTestId("qa-slide-join");

    // The top five open questions.
    public ILocator Rows => slide.GetByTestId("qa-slide-rows");

    public ILocator Footer => slide.GetByTestId("qa-slide-footer");

    // "Open" or "Closed"
    public ILocator Phase => slide.GetByTestId("qa-slide-phase");

    // "6 questions"
    public ILocator Total => slide.GetByTestId("qa-slide-total");

    // "Top 5 of 7, by upvotes" or "Ask and upvote on your phone"
    public ILocator FooterNote => slide.GetByTestId("qa-slide-footer-note");

    // One question up, at full size, in place of the list.
    public ILocator OneUp => slide.GetByTestId("qa-slide-one-up");

    public ILocator OneUpText => slide.GetByTestId("qa-slide-one-up-text");

    public ILocator OneUpVotes => slide.GetByTestId("qa-slide-one-up-votes");

    // The name or "Anonymous"
    public ILocator OneUpName => slide.GetByTestId("qa-slide-one-up-name");

    // "On screen now · 4 more waiting"
    public ILocator OneUpMore => slide.GetByTestId("qa-slide-one-up-more");

    // "No questions yet"
    public ILocator Empty => slide.GetByTestId("qa-slide-empty");

    // Closed and empty: "Questions are closed"
    public ILocator Closed => slide.GetByTestId("qa-slide-closed");

    // "Questions from the room appear here"
    public ILocator NotLive => slide.GetByTestId("qa-slide-not-live");

    // "Questions are off": the title and this line, when the room has no round (questions off) on a live deck.
    public ILocator Off => slide.GetByTestId("qa-slide-off");

    // "Not live"
    public ILocator NotLivePill => slide.GetByTestId("qa-slide-not-live-pill");

    // Question {number}'s row in the list: the row, its text (one line, cut with an ellipsis) and its votes.
    public ILocator Row(int number) => slide.GetByTestId($"qa-slide-row-{number}");

    public ILocator RowText(int number) => slide.GetByTestId($"qa-slide-row-text-{number}");

    public ILocator RowVotes(int number) => slide.GetByTestId($"qa-slide-row-votes-{number}");

    // The question card over any slide, on the page.
    public ILocator CardLayer => slide.Page.GetByTestId("qa-card-layer");

    public ILocator Card => slide.Page.GetByTestId("qa-card");

    // "Question from the room"
    public ILocator CardLabel => slide.Page.GetByTestId("qa-card-label");

    // "▲ 9"
    public ILocator CardVotes => slide.Page.GetByTestId("qa-card-votes");

    // The name or "Anonymous"
    public ILocator CardName => slide.Page.GetByTestId("qa-card-name");

    public ILocator CardText => slide.Page.GetByTestId("qa-card-text");

    // The floating reactions up the right edge, on the page.
    public ILocator ReactionsLayer => slide.Page.GetByTestId("qa-reactions-layer");

    // One floating emoji; at most 30 on screen at once.
    public ILocator ReactionFloats => slide.Page.GetByTestId("qa-reactions-float");

    // "+14 👏", the rest of a batch past 30, and the only thing drawn with reduced motion.
    public ILocator ReactionsChip => slide.Page.GetByTestId("qa-reactions-chip");
}
