using System.Text.Json;

namespace SlideWorks.WebApi.Features.Presenting.Views;

// One slide in the presenter view (presenter.json, P3): the play view's slide plus its notes and headline. Notes are
// plain text with newlines, where "**bold**" and lines starting "- " are the only markup. Headline is the deck page's
// headline for the slide (DeckPageSlide's rule). Slots are the stored slots, whose shape depends on the layout.
public sealed record PresenterSlide(
    int Number,
    string Layout,
    int? Part,
    bool DeepDive,
    int Steps,
    JsonElement Slots,
    string? Notes,
    string? Headline);
