namespace SlideWorks.Core.Translations;

// One translatable line of a slide (K3): an RFC 6901 pointer into the stored slots and the primary text
// at it.
public sealed record SlotLine(string Path, string Text);
