namespace SlideWorks.Core.Translations;

// One line of a merged slide (K3): its path, its primary text, the stored translation (current or
// stale, null when missing) and its state.
public sealed record LineView(string Path, string Primary, string? Translation, LineState State);
