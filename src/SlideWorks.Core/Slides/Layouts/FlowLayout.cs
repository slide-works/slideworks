using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class FlowLayout
{
    // The geometry is the client's FlowLayout.tsx and FlowLayout.module.css, number for number: a 1680 px body
    // with a 72 px column for each arrow; a plain node is 1 fr and a node with items 2 fr, so a node is
    // (1680 - 72 x (n - 1)) / total fr x its fr wide, with 30 px padding each side (inner width = width - 60);
    // a node is 60 px of padding, a label of 28 px, the title at 32 px (38.4 px lines), the text 12 px under it
    // at 24 px (33.6 px lines), and a group's tiles 18 px under that: two columns 14 px apart, each tile
    // (inner - 14) / 2 wide with 41 px of padding and rule, 22 px text (27.5 px lines) in 32 px of padding, rows
    // 14 px apart. Under the nodes: 34 px, the caption at 24 px (32.4 px lines), 58 px of chips; then the callout.
    private const double BODY_WIDTH = 1680;
    private const double ARROW_COLUMN = 72;
    private const double PADDING = 60;
    private const double LABEL = 28;
    private const double TITLE_FONT = 32;
    private const double TITLE_LINE = 38.4;
    private const double TEXT_GAP = 12;
    private const double TEXT_FONT = 24;
    private const double TEXT_LINE = 33.6;
    private const double ITEMS_GAP = 18;
    private const double TILE_GAP = 14;
    private const double TILE_SIDE = 41;
    private const double TILE_PADDING = 32;
    private const double TILE_FONT = 22;
    private const double TILE_LINE = 27.5;
    private const double CAPTION_GAP = 34;
    private const double CAPTION_FONT = 24;
    private const double CAPTION_LINE = 32.4;
    private const double CHIPS = 58;

    public static LayoutDefinition Definition { get; } = new("flow", "2–4 nodes joined by arrows, left to right. A node is a card with a title; one node can hold 2–4 items and is drawn as a box of tiles. Can reveal one node per step.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("nodes", new ListSlot(Obj(
            Req("title", new TextSlot(30)),
            Opt("label", new TextSlot(20), "A small line above the title"),
            Opt("text", new TextSlot(120)),
            Opt("items", new ListSlot(new TextSlot(28), 2, 4), "Draws the node as a box of tiles; one node only"),
            Opt("tone", Tone(null), "'inverse' and 'accent' fill the card; 'positive' and 'negative' colour its rule")), 2, 4), "Left to right"),
        Opt("caption", new TextSlot(140)),
        Opt("chips", new ListSlot(new BlockSlot(InlineBlocks.Chip), 1, 4)),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.List("nodes"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var nodes = slots.GetProperty("nodes").EnumerateArray().ToArray();
        var holders = Enumerable.Range(0, nodes.Length).Where(i => ObjectSlot.IsPresent(nodes[i], "items", out _)).ToArray();
        if (holders.Length > 1)
        {
            validation.Fail($"{path}.nodes[{holders[1]}].items", "only one node can hold items");
            return;
        }

        if (holders.Length == 1 && nodes.Length > 3)
        {
            validation.Fail($"{path}.nodes", "at most 3 nodes when one holds items");
            return;
        }

        var total = nodes.Length + holders.Length;
        var unit = (BODY_WIDTH - (ARROW_COLUMN * (nodes.Length - 1))) / total;
        var tallest = nodes.Max(n => NodeHeight(n, Math.Floor(unit * (ObjectSlot.IsPresent(n, "items", out _) ? 2 : 1)) - PADDING, validation));
        var height = tallest;
        var hasCaption = ObjectSlot.IsPresent(slots, "caption", out var caption);
        var hasChips = ObjectSlot.IsPresent(slots, "chips", out _);
        if (hasCaption || hasChips)
        {
            height += CAPTION_GAP;
        }

        if (hasCaption)
        {
            height += SlideFit.Lines(caption.GetString()!, BODY_WIDTH, CAPTION_FONT, validation.Widths.Body) * CAPTION_LINE;
        }

        height += hasChips ? CHIPS : 0;
        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            height += LayoutRules.CalloutHeight(callout, BODY_WIDTH, validation.Widths);
        }

        SlideFit.Check(height, $"{path}.nodes", validation);
    }

    private static double NodeHeight(JsonElement node, double inner, SlotValidation validation)
    {
        var height = PADDING + (ObjectSlot.IsPresent(node, "label", out _) ? LABEL : 0);
        height += SlideFit.Lines(node.GetProperty("title").GetString()!, inner, TITLE_FONT, validation.Widths.Head) * TITLE_LINE;
        if (ObjectSlot.IsPresent(node, "text", out var text))
        {
            height += TEXT_GAP + (SlideFit.Lines(text.GetString()!, inner, TEXT_FONT, validation.Widths.Body) * TEXT_LINE);
        }

        if (ObjectSlot.IsPresent(node, "items", out var items))
        {
            var tile = Math.Floor(((inner - TILE_GAP) / 2) - TILE_SIDE);
            var tiles = items.EnumerateArray().Select(i => SlideFit.Lines(i.GetString()!, tile, TILE_FONT, validation.Widths.Body)).ToArray();
            var rows = tiles.Chunk(2).Select(row => TILE_PADDING + (row.Max() * TILE_LINE)).ToArray();
            height += ITEMS_GAP + rows.Sum() + (TILE_GAP * (rows.Length - 1));
        }

        return height;
    }
}
