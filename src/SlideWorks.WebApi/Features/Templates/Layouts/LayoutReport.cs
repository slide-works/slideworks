namespace SlideWorks.WebApi.Features.Templates.Layouts;

// What the review page measured in the owner's browser and sends with an approval: the layout asset the owner saw, the
// slots that overflow the safe area and the colour pairs with too little contrast.
public sealed record LayoutReport(string Asset, IReadOnlyList<string> Overflow, IReadOnlyList<string> Contrast);
