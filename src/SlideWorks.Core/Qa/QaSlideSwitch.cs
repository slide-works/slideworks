namespace SlideWorks.Core.Qa;

// One slide's switch (Q1, D10): while the slide is on screen it can turn asking, reactions or both off. Both are true by
// default; a row exists only for a slide with something off. It can only turn off what the deck has on (QaRoomRules).
public sealed record QaSlideSwitch(bool Questions, bool Reactions)
{
    // A slide with no switch row: it follows the deck.
    public static QaSlideSwitch On { get; } = new(true, true);
}
