using System.Text.Json;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Slides;

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
    string? Headline)
{
    // Notes are the stored notes, with no deep-dive prefix: the presenter view shows the deep-dive flag itself (SW-175 D4).
    public static PresenterSlide From(Slide slide)
    {
        var slots = SlideJson.Slots(slide);
        return new PresenterSlide(
            slide.Position + 1,
            slide.Layout,
            slide.Part,
            slide.DeepDive,
            slide.Steps,
            slots,
            slide.Notes,
            DeckPageSlide.HeadlineOf(slide.Layout, slots));
    }
}
