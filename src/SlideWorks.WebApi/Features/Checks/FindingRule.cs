namespace SlideWorks.WebApi.Features.Checks;

// What a finding is about. On the wire each is its camelCase name ("lowContrast"). The order is the sort order within a severity.
public enum FindingRule
{
    Overflow,
    Fit,
    ImageNotLoaded,
    RenderFailed,
    SmallText,
    LowContrast,
    ImageLowResolution,
    GenericAlt,
    LayoutOutsideTemplate,
    TextOnImage,
}
