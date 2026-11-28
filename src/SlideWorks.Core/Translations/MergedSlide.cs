using System.Text.Json;

namespace SlideWorks.Core.Translations;

// A slide's slots with one language applied (K3): the merged slots, how many lines show the primary
// text because their translation is missing or stale, and every line with its state.
public sealed record MergedSlide(JsonElement Slots, int FallbackLines, IReadOnlyList<LineView> Lines);
