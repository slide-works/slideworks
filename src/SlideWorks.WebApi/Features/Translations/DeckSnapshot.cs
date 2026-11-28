using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Translations;

// What TranslationMerge loaded for a deck: its settings, its template and its live slides in position order.
public sealed record DeckSnapshot(TranslationSettings Settings, ResolvedTemplate Template, IReadOnlyList<Slide> Slides);
