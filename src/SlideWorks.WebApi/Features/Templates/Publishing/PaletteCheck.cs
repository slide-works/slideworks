using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// The palette passes every contrast pair, and the extra colours have an ink that reads on them.
public sealed class PaletteCheck : IPublishCheck
{
    private const string KEY = "palette";
    private const string LABEL = "Palette passes contrast";

    public CheckOutcome Run(PublishFacts facts)
    {
        var findings = ManifestPaletteCheck.Check(facts.Manifest).Concat(ExtraColours.Check(facts.Manifest)).ToList();
        return findings.Count == 0
            ? CheckOutcome.Pass(KEY, LABEL, "All pairs pass", "palette")
            : CheckOutcome.Fail(KEY, LABEL, "palette", findings[0].Code, findings[0].Path, findings[0].Message);
    }
}
