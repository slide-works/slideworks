using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Translations;

// A deck's live slides merged in one language (TranslationMerge.ForDeckAsync): Slides in the deck's position order, and a
// lookup by slide.
public sealed class MergedDeck
{
    private readonly Dictionary<Guid, MergedSlide> _bySlide;

    public MergedDeck(IReadOnlyList<(Guid SlideId, MergedSlide Merged)> inOrder)
    {
        Slides = [.. inOrder.Select(entry => entry.Merged)];
        _bySlide = inOrder.ToDictionary(entry => entry.SlideId, entry => entry.Merged);
    }

    public IReadOnlyList<MergedSlide> Slides { get; }

    // The merged slide, or null when it is not a live slide of the deck.
    public MergedSlide? Of(Slide slide) => _bySlide.GetValueOrDefault(slide.Id);
}
