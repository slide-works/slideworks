namespace SlideWorks.Core.Look.CustomLayouts;

public sealed class LayoutSamples : ILayoutSamples
{
    // TODO: refuses until the sample writer is written; the real bodies build slots from the schema.
    // Nothing reaches these while the stages before them refuse every upload.
    public string Sample(CustomLayoutSchema schema) =>
        throw new NotSupportedException(CustomLayoutErrorCodes.Text(CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE));

    public string Maximum(CustomLayoutSchema schema) =>
        throw new NotSupportedException(CustomLayoutErrorCodes.Text(CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE));
}
