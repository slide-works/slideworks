namespace SlideWorks.WebApi.Realtime.Presenting;

// What every window and phone of one presentation shares (P1, D4): the server holds it and clients only render it. The
// sample is src/contracts/presentation-state.json. Epoch is a random id given when the state is created; Seq goes up by
// one on every accepted change within an epoch. Slide is 1-based and Step 0-based, as the player's #slide.step (K12).
// TalkMinutes is the deck's talk length (P7) or null. Connected lists presenter windows, then audience windows, then
// phones, each in join order. It never carries notes (P9).
public sealed record PresentationState(
    string DeckId,
    string Epoch,
    long Seq,
    int Slide,
    int Step,
    bool Black,
    bool SkipDeepDives,
    PresentationTimer Timer,
    int? TalkMinutes,
    IReadOnlyList<PresentationConnection> Connected);
