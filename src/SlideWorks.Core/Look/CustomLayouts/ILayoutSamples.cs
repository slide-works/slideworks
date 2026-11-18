namespace SlideWorks.Core.Look.CustomLayouts;

// Slots JSON a layout's owner reviews it with: ordinary content, and every slot at its limit.
// Both must pass the layout's own definition.
public interface ILayoutSamples
{
    string Sample(CustomLayoutSchema schema);

    string Maximum(CustomLayoutSchema schema);
}
