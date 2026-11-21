namespace SlideWorks.WebApi.Features.Checks;

// check_deck's answer. Measured is false when the render checks didn't run (or stopped early); Error then says why, or is null when the caller asked for stored checks only.
public sealed record DeckChecksResult(string Deck, IReadOnlyList<int> Slides, IReadOnlyList<SlideFinding> Findings, bool Measured, string? Error);
