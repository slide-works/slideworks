using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// The manifest's counts are within the template limits.
public sealed class LimitsCheck : IPublishCheck
{
    private const string KEY = "limits";
    private const string LABEL = "Within the limits";

    public CheckOutcome Run(PublishFacts facts)
    {
        var findings = ManifestLimits.Check(facts.Manifest);
        return findings.Count == 0
            ? CheckOutcome.Pass(KEY, LABEL, "", "basics")
            : CheckOutcome.Fail(KEY, LABEL, "basics", findings[0].Code, findings[0].Path, findings[0].Message);
    }
}
