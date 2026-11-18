namespace SlideWorks.WebApi.Features.Templates.Publishing;

// The checklist, in the order the editor shows it. Wave 2 adds the layouts row.
public static class PublishChecks
{
    private static readonly IReadOnlyList<IPublishCheck> All =
    [
        new PaletteCheck(),
        new FontsCheck(),
        new FontRightsCheck(),
        new LogoArtCheck(),
        new LimitsCheck(),
    ];

    public static IReadOnlyList<CheckOutcome> Run(PublishFacts facts) => [.. All.Select(check => check.Run(facts))];
}
