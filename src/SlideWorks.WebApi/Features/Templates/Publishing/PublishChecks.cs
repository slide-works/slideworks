namespace SlideWorks.WebApi.Features.Templates.Publishing;

// The checklist, in the order the editor shows it.
public static class PublishChecks
{
    private static readonly IReadOnlyList<IPublishCheck> All =
    [
        new PaletteCheck(),
        new FontsCheck(),
        new FontRightsCheck(),
        new LogoArtCheck(),
        new LimitsCheck(),
        new LayoutsCheck(),
    ];

    public static IReadOnlyList<CheckOutcome> Run(PublishFacts facts) => [.. All.Select(check => check.Run(facts))];
}
