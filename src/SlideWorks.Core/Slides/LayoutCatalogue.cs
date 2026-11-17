namespace SlideWorks.Core.Slides;

// The 18 layouts and 5 inline blocks (K2, D8). Names and slot fields are frozen in the plan's
// section 4. The limits come from the mockup CSS so that a slide at its limits fits 1920×1080: a
// headline wraps to at most two lines, and where counts times lengths could overflow, a fit rule in
// LayoutRules checks the text the slide actually has.
public static class LayoutCatalogue
{
    private const int KICKER = 40;
    private const int HEADLINE = 80;

    private const string REVEAL = "'one-by-one' shows one item per step; 'all' (the default) shows them all at once";

    private const string TONE = "Colours from the deck's palette";
    private const string FILL = "'soft' (the default) tints it; 'solid' fills it with the tone's colour";

    public static readonly InlineBlock Callout = new(
        "callout",
        "A highlighted sentence with a tone (accent, positive, negative or inverse) and a soft or solid fill.",
        Obj(Req("tone", Tone(null), TONE), Opt("fill", Fill(), FILL), Req("text", new TextSlot(160))));

    public static readonly InlineBlock Chip = new("chip", "A small rounded tag, such as a tool or a practice.", new TextSlot(24));

    public static readonly InlineBlock Badge = new("badge", "A short label at the top of a card, such as a role.", new TextSlot(20));

    public static readonly InlineBlock Stamp = new("stamp", "A tilted stamp on the title slide, such as 'Draft'.", new TextSlot(16));

    public static readonly InlineBlock Journey = new(
        "journey",
        "The parts of the deck as a timeline on divider slides. Rendered from the deck's parts; agents never set it.",
        null);

    public static IReadOnlyList<InlineBlock> Blocks { get; } = [Callout, Chip, Badge, Stamp, Journey];

    public static IReadOnlyList<LayoutDefinition> Layouts { get; } =
    [
        new("title", "Opening slide (dark): the deck's title with a kicker, subtitle, tagline, byline and an optional stamp.", true,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("title", new TextSlot(34), "Set at 168 px: two lines at most, shrinking to 96 px to keep them"),
            Opt("subtitle", new TextSlot(100)),
            Opt("tagline", new TextSlot(90)),
            Opt("author", new TextSlot(60), "The presenter's name"),
            Opt("affiliation", new TextSlot(60), "The presenter's team or organisation"),
            Opt("stamp", new BlockSlot(Stamp)),
        ]),
        new("divider", "Section divider (dark) that opens a part. Its number and the journey come from the deck's parts and the slide's part.", true,
        [
            Req("title", new TextSlot(44), "Set at 104 px: two lines at most, shrinking to 64 px to keep them"),
            Opt("sub", new TextSlot(80)),
        ]),
        new("bullets", "A headline over 1–8 points, each plain text or a bold lead plus text. Can reveal one point per step.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("items", new ListSlot(LeadOrText(200, 60, 160), 1, 8), "Each point is a string, or {lead, text} for a bold lead-in"),
            Opt("reveal", Reveal(), REVEAL),
            Opt("callout", new BlockSlot(Callout)),
        ], stepsFrom: "items", rules: LayoutRules.Bullets),
        new("two-column", "A headline over two columns. Each column holds a list of points, a paragraph or an image.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("left", Column(), "Exactly one of items, text or image"),
            Req("right", Column(), "Exactly one of items, text or image"),
        ], rules: LayoutRules.TwoColumn),
        new("practice", "A practice: what to do, what comes back and what to avoid, with an optional callout.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("do", new ListSlot(Obj(Req("lead", new TextSlot(50)), Req("text", new TextSlot(120))), 1, 5)),
            Req("comes_back", new ListSlot(new TextSlot(100), 0, 4), "What the team gets back; may be empty"),
            Req("avoid", new ListSlot(new TextSlot(100), 0, 4), "What to avoid; may be empty"),
            Opt("callout", new BlockSlot(Callout)),
        ], rules: LayoutRules.Practice),
        new("compare", "Two panels side by side: the failure mode and the success mode.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("failure", ComparePanel("negative")),
            Req("success", ComparePanel("positive")),
        ], rules: LayoutRules.Compare),
        new("big-number", "One number that carries the slide, with a caption, a detail and a source.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("value", new TextSlot(6), "The number with a short unit, such as '11 wks' or '42%'; the layout sets a unit smaller than the number"),
            Req("caption", new TextSlot(120)),
            Opt("detail", new TextSlot(200)),
            Opt("source", new TextSlot(100), "Where the number comes from, without a 'Source:' prefix"),
        ]),
        new("quote", "A quotation with its attribution.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("quote", new TextSlot(280), "The words only; the layout adds the quotation marks"),
            Req("attribution", new TextSlot(80)),
        ]),
        new("image", "A headline over one large image, with optional points beside it and a caption.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("image", new ImageSlot()),
            Opt("points", new ListSlot(new TextSlot(80), 1, 4)),
            Opt("caption", new TextSlot(140)),
        ]),
        new("table", "A table of 2–6 columns and 1–6 rows. A cell can carry a tone, and one row can be highlighted.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("columns", new ListSlot(new TextSlot(30), 2, 6)),
            Req("rows", new ListSlot(new ListSlot(TableCell(), 2, 6), 1, 6), "Each row has one cell per column"),
            Opt("highlightRow", new NumberSlot(1, 6, true), "The 1-based number of the row to highlight"),
        ], rules: LayoutRules.Table),
        new("chart", "A line or bar chart of 1–3 series, with an optional marker line and caption.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("kind", new EnumSlot("line", "bar")),
            Req("xLabels", new ListSlot(new TextSlot(12), 2, 12)),
            Req("series", new ListSlot(Obj(
                Req("name", new TextSlot(30)),
                Req("values", new ListSlot(new NumberSlot(-1_000_000_000, 1_000_000_000, false), 2, 12), "One number per xLabel")), 1, 3)),
            Opt("yUnit", new TextSlot(8), "A short unit for the y axis, such as 'wk' or '%'"),
            Opt("caption", new TextSlot(160)),
            Opt("marker", Obj(
                Req("x", new NumberSlot(1, 12, false), "1 is the first xLabel; 3.5 sits between the third and the fourth"),
                Req("label", new TextSlot(20))), "A dashed vertical line with a label"),
        ], rules: LayoutRules.Chart),
        new("gantt", "A timeline of 1–5 rows across 2–8 columns. Each bar has a status: done, slips, stopped or ship.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("columns", new ListSlot(new TextSlot(16), 2, 8)),
            Req("rows", new ListSlot(Obj(
                Req("label", new TextSlot(30)),
                Req("start", new NumberSlot(1, 8, true), "The 1-based column the bar starts in"),
                Req("end", new NumberSlot(1, 8, true), "The 1-based column the bar ends in (inclusive)"),
                Req("status", new EnumSlot("done", "slips", "stopped", "ship"), "'ship' draws a ship marker instead of a bar"),
                Opt("note", new TextSlot(40), "Text on the bar; defaults to the status")), 1, 5)),
            Opt("caption", new TextSlot(140)),
        ], rules: LayoutRules.Gantt),
        new("numbered-cards", "2–4 numbered cards, each a title and text. Can reveal one card per step.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("cards", new ListSlot(Obj(Req("title", new TextSlot(40)), Req("text", new TextSlot(140))), 2, 4)),
            Opt("reveal", Reveal(), REVEAL),
        ], stepsFrom: "cards"),
        new("rules", "1–4 numbered rules, each a title and text. Can reveal one rule per step.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("rules", new ListSlot(Obj(Req("title", new TextSlot(60)), Req("text", new TextSlot(110))), 1, 4)),
            Opt("reveal", Reveal(), REVEAL),
        ], stepsFrom: "rules", rules: LayoutRules.Rules),
        new("cards", "1–3 cards, each with an optional badge, a title, text and chips. Can reveal one card per step.", false,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Req("cards", new ListSlot(Obj(
                Opt("badge", new BlockSlot(Badge)),
                Req("title", new TextSlot(50)),
                Req("text", new TextSlot(140)),
                Req("chips", new ListSlot(new BlockSlot(Chip), 0, 4), "May be empty"),
                Opt("tone", Tone(null), TONE + "; left out, the card is plain"),
                Opt("fill", Fill(), FILL)), 1, 3)),
            Opt("reveal", Reveal(), REVEAL),
        ], stepsFrom: "cards"),
        new("close", "Closing slide (dark): a short headline of up to three written lines, with an optional sub and author.", true,
        [
            Req("headline", new TextSlot(80, lineBreaks: true), "Set at 112 px, about 22 characters a line: use \\n for a line break, at most 3 written and 4 shown lines; it shrinks to 60 px to keep 4 lines"),
            Opt("sub", new TextSlot(110)),
            Opt("author", new TextSlot(60)),
        ], rules: LayoutRules.Close),
        new("join", "Join slide (dark): a QR code, the join code and the address, so the room can follow the talk on their phones. The code and the QR come from the live session, never from the slots.", true,
        [
            Opt("kicker", new TextSlot(KICKER), "Defaults to 'Join on your phone'"),
            Opt("count", new EnumSlot("shown", "hidden") { Default = "shown" }, "'shown' (the default) shows how many have joined; 'hidden' leaves the count off"),
        ]),
        new("poll", "Poll slide: a question and 2 to 8 options that the room answers on their phones while the deck is live. The poll's settings (how many picks, when results show, the correct answer, when it opens) are set with update_poll, never in the slots.", false,
        [
            Req("question", new TextSlot(120)),
            Req("options", new ListSlot(new TextSlot(60), 2, 8), "Known by position: while a round is open they can be reworded or added to, not removed or reordered. Each option needs its own text: two that match, ignoring case and spaces at the ends, are refused"),
        ], rules: LayoutRules.Poll),
    ];

    public static IReadOnlyList<string> LayoutNames { get; } = Layouts.Select(l => l.Name).ToArray();

    public static LayoutDefinition? Find(string name) =>
        Layouts.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.Ordinal));

    private static SlotField Req(string name, ISlotType type, string? description = null) => new(name, type, true, description);

    private static SlotField Opt(string name, ISlotType type, string? description = null) => new(name, type, false, description);

    private static ObjectSlot Obj(params SlotField[] fields) => new(fields);

    private static EnumSlot Reveal() => new("all", "one-by-one");

    private static TextOrObjectSlot LeadOrText(int text, int leadInObject, int textInObject) =>
        new(new TextSlot(text), Obj(Req("lead", new TextSlot(leadInObject)), Req("text", new TextSlot(textInObject))));

    private static ObjectSlot Column() => new(
        [
            Opt("title", new TextSlot(50)),
            Opt("items", new ListSlot(LeadOrText(120, 40, 120), 1, 6)),
            Opt("text", new TextSlot(400)),
            Opt("image", new ImageSlot()),
        ],
        exactlyOneOf: ["items", "text", "image"]);

    private static ObjectSlot ComparePanel(string defaultTone) => Obj(
        Req("label", new TextSlot(24), "The panel's tag, such as 'Failure mode'"),
        Req("title", new TextSlot(50)),
        Opt("sub", new TextSlot(100)),
        Req("points", new ListSlot(new TextSlot(90), 1, 4)),
        Opt("tone", Tone(defaultTone), $"{TONE}; defaults to '{defaultTone}'"),
        Opt("fill", Fill(), FILL));

    // Callout, card and compare-panel tones (L4). Table cell tones are a separate, unchanged set.
    private static EnumSlot Tone(string? defaultTone) => new("accent", "positive", "negative", "inverse")
    {
        Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["good"] = "positive",
            ["bad"] = "negative",
            ["info"] = "accent",
        },
        Default = defaultTone,
    };

    private static EnumSlot Fill() => new("soft", "solid") { Default = "soft" };

    private static TextOrObjectSlot TableCell() => new(
        new TextSlot(40),
        Obj(Req("text", new TextSlot(40)), Req("tone", new EnumSlot("good", "warn", "bad", "info"), "Draws the cell as a badge in this tone")));
}
