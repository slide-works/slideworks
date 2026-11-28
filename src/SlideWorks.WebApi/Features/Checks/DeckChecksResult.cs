namespace SlideWorks.WebApi.Features.Checks;

// check_deck's answer. Languages are the language codes checked, in the order checked, and empty while translations are off.
// Measured is false when the render checks didn't run (or stopped early); Error then says why, or is null when the caller asked for stored checks only.
public sealed record DeckChecksResult(string Deck, IReadOnlyList<int> Slides, IReadOnlyList<string> Languages, IReadOnlyList<SlideFinding> Findings, bool Measured, string? Error);
