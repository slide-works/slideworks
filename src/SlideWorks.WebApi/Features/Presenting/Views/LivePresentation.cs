namespace SlideWorks.WebApi.Features.Presenting.Views;

// One of the signed-in person's live presentations (presentations.json, P4): text only, no slide content. Total is the
// deck's slide count; StartedLabel is formatted on the server from when the state was created.
public sealed record LivePresentation(
    string DeckId,
    string Title,
    int Slide,
    int Total,
    string StartedLabel,
    IReadOnlyList<LiveWindow> Windows);
