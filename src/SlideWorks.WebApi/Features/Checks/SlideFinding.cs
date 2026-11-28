namespace SlideWorks.WebApi.Features.Checks;

// One problem (or note) on one slide. Where names the thing, such as a text or a slot; Message says what to change.
// Language is the language the slide was checked in: null while the deck's translations are off, and always null on the deck page.
public sealed record SlideFinding(int Slide, FindingRule Rule, FindingSeverity Severity, FindingSource Source, string Where, string Message, string? Language = null)
{
    // The fixed severity of each rule.
    public static FindingSeverity SeverityOf(FindingRule rule) => rule switch
    {
        FindingRule.Overflow or FindingRule.Fit or FindingRule.ImageNotLoaded or FindingRule.RenderFailed => FindingSeverity.Error,
        FindingRule.TextOnImage => FindingSeverity.Note,
        _ => FindingSeverity.Warning,
    };

    // Slide, then severity (errors first), then rule.
    public static IReadOnlyList<SlideFinding> Sorted(IEnumerable<SlideFinding> findings)
        => [.. findings.OrderBy(f => f.Slide).ThenBy(f => f.Severity).ThenBy(f => f.Rule)];
}
