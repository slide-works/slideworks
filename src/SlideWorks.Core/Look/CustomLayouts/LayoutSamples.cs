using SlideWorks.Core.Look.CustomLayouts.Schema;

namespace SlideWorks.Core.Look.CustomLayouts;

// The slots JSON the review page shows (T10): pure and deterministic, so the same schema always gives the
// same text. The reveal slot is left out of both, and so is every image.
public sealed class LayoutSamples : ILayoutSamples
{
    public string Sample(CustomLayoutSchema schema) => SlotContent.Write(SlotContent.TypesOf(schema), maximum: false);

    public string Maximum(CustomLayoutSchema schema) => SlotContent.Write(SlotContent.TypesOf(schema), maximum: true);
}
