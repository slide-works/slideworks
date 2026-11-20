using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.Core.Slides;

// The layouts and 5 inline blocks (K2, D8). Names and slot fields are frozen in the plan's
// section 4. Each layout's definition and rule live in their own file under Layouts/, the blocks in
// InlineBlocks. The limits come from the mockup CSS so that a slide at its limits fits 1920×1080: a
// headline wraps to at most two lines, and where counts times lengths could overflow, the layout's
// fit rule checks the text the slide actually has.
public static class LayoutCatalogue
{
    public static readonly InlineBlock Callout = InlineBlocks.Callout;

    public static readonly InlineBlock Chip = InlineBlocks.Chip;

    public static readonly InlineBlock Badge = InlineBlocks.Badge;

    public static readonly InlineBlock Stamp = InlineBlocks.Stamp;

    public static readonly InlineBlock Journey = InlineBlocks.Journey;

    public static IReadOnlyList<InlineBlock> Blocks => InlineBlocks.All;

    public static IReadOnlyList<LayoutDefinition> Layouts { get; } =
    [
        TitleLayout.Definition,
        DividerLayout.Definition,
        BulletsLayout.Definition,
        TwoColumnLayout.Definition,
        PracticeLayout.Definition,
        CompareLayout.Definition,
        BigNumberLayout.Definition,
        QuoteLayout.Definition,
        ImageLayout.Definition,
        VideoLayout.Definition,
        TableLayout.Definition,
        ChartLayout.Definition,
        GanttLayout.Definition,
        NumberedCardsLayout.Definition,
        RulesLayout.Definition,
        CardsLayout.Definition,
        StatementLayout.Definition,
        LinksLayout.Definition,
        StackLayout.Definition,
        BarsLayout.Definition,
        FlowLayout.Definition,
        LevelsLayout.Definition,
        ColumnsListLayout.Definition,
        TimelineLayout.Definition,
        LoopLayout.Definition,
        CloseLayout.Definition,
        JoinLayout.Definition,
        PollLayout.Definition,
        QaLayout.Definition,
    ];

    public static IReadOnlyList<string> LayoutNames { get; } = Layouts.Select(l => l.Name).ToArray();

    public static LayoutDefinition? Find(string name) =>
        Layouts.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.Ordinal));
}
