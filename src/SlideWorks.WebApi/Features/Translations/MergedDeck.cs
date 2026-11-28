using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Translations;

// A deck's live slides merged in one language (TranslationMerge.ForDeckAsync), found by slide, in the deck's order.
public sealed class MergedDeck(IReadOnlyDictionary<Guid, MergedSlide> bySlide)
{
    public IEnumerable<MergedSlide> Slides => bySlide.Values;

    // The merged slide, or null when it is not a live slide of the deck.
    public MergedSlide? Of(Slide slide) => bySlide.GetValueOrDefault(slide.Id);
}
