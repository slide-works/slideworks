namespace SlideWorks.AcceptanceTests.Journeys.Rebuild;

// Slides 1–22 of the 44-slide test deck, rebuilt as data: made-up Northwind text at every slot's documented maximum, every list at
// the count seen on the compare sheet. Slides 9, 11 and 13 are custom layouts (see SW-489) and are not here. Number is the
// slide's number in the original deck; Part is the 1-based part to add it to (null for none); Callouts and Steps are what the
// original had and what the layout reports; Surface is the add_slide surface (dark for the dark quote); Photo is true when the original is a photo or a screenshot, so an img is expected.
public static class FirstHalfSlides
{
    public static readonly IReadOnlyList<(int Number, string Layout, int? Part, string Kind, bool Photo, int Callouts, int Steps, string? Surface, Func<string, object> Slots)> ALL =
    [
        (1, "title", null, "opening title", false, 0, 0, null, _ => new
        {
            kicker = M(60, 1), title = M(34, 2), subtitle = M(100, 3), tagline = M(90, 4), author = M(60, 5), affiliation = M(60, 6),
        }),
        (2, "cards", null, "three cards", false, 0, 3, null, _ => new
        {
            kicker = M(60, 7), headline = M(100, 8), reveal = "one-by-one",
            cards = new object[]
            {
                new { icon = "coins", badge = M(36, 9), title = M(70, 10), text = M(140, 11), tone = "accent", fill = "plain" },
                new { icon = "cycle", badge = M(36, 12), title = M(70, 13), text = M(140, 14), tone = "positive", fill = "plain" },
                new { icon = "warning", badge = M(36, 15), title = M(70, 16), text = M(140, 17), tone = "negative", fill = "plain" },
            },
        }),
        (3, "divider", 1, "part divider", false, 0, 0, null, _ => new { title = M(44, 18), sub = M(110, 19) }),
        (4, "cards", 1, "2x2 cards, one lit", false, 0, 0, null, _ => new
        {
            kicker = M(60, 20), headline = M(100, 21),
            cards = new object[]
            {
                new { title = M(70, 22), text = M(140, 23), tone = "accent", fill = "solid", pin = M(24, 24) },
                new { title = M(70, 25), text = M(140, 26), fill = "plain" },
                new { title = M(70, 27), text = M(140, 28), fill = "plain" },
                new { title = M(70, 29), text = M(140, 30), fill = "plain" },
            },
        }),
        // shortened: list items 110, not 120, callout 190, not 200 (SW-802 estimate)
        (5, "two-column", 1, "list, big word, callout", false, 1, 0, null, _ => new
        {
            kicker = M(60, 31), headline = M(100, 32),
            left = new
            {
                items = new object[]
                {
                    new { icon = "calendar", text = M(110, 34) },
                    new { icon = "cycle", text = M(110, 36) },
                    new { icon = "list", text = M(110, 38) },
                },
            },
            right = new { title = M(60, 39), icon = "search", text = M(400, 40) },
            callout = new { tone = "accent", text = M(190, 41) },
        }),
        (6, "compare", 1, "two kinds, side by side", false, 0, 0, null, _ => new
        {
            kicker = M(60, 42), headline = M(100, 43),
            failure = new
            {
                label = M(24, 44), title = M(50, 45), icon = "factory", tone = "inverse", fill = "solid",
                rows = new object[] { new { label = M(16, 46), value = M(40, 47) }, new { label = M(16, 48), value = M(40, 49) }, new { label = M(16, 50), value = M(40, 51) } },
            },
            success = new
            {
                label = M(24, 52), title = M(50, 53), icon = "people", tone = "accent", fill = "solid",
                rows = new object[] { new { label = M(16, 54), value = M(40, 55) }, new { label = M(16, 56), value = M(40, 57) }, new { label = M(16, 58), value = M(40, 59) } },
            },
        }),
        (7, "stack", 1, "stacked column", false, 1, 6, null, _ => new
        {
            kicker = M(60, 60), headline = M(100, 61), reveal = "one-by-one",
            segments = new object[]
            {
                new { label = M(40, 62), value = 12 },
                new { label = M(40, 64), value = 6 },
                new { label = M(40, 66), value = 8 },
                new { label = M(40, 68), value = 10 },
                new { label = M(40, 70), value = 14 },
                new { label = M(40, 72), value = 50 },
            },
            total = M(30, 74), caption = M(60, 75),
            callout = new { tone = "accent", text = M(220, 76) },
        }),
        (8, "divider", 2, "part divider", false, 0, 0, null, _ => new { title = M(44, 77), sub = M(110, 78) }),
        (10, "divider", 3, "part divider", false, 0, 0, null, _ => new { title = M(44, 79), sub = M(110, 80) }),
        (12, "divider", 4, "part divider", false, 0, 0, null, _ => new { title = M(44, 81), sub = M(110, 82) }),
        (14, "stack", 4, "stacked column, one new", false, 1, 0, null, _ => new
        {
            kicker = M(60, 83), headline = M(100, 84),
            segments = new object[]
            {
                new { label = M(30, 85), value = 8, tone = "accent", tag = M(12, 87) },
                new { label = M(30, 88), value = 12 },
                new { label = M(30, 90), value = 6 },
                new { label = M(30, 91), value = 8 },
                new { label = M(30, 92), value = 10 },
                new { label = M(30, 93), value = 14 },
                new { label = M(30, 94), value = 42 },
            },
            total = M(30, 95), caption = M(60, 96),
            points = new object[] { new { lead = M(8, 97), text = M(90, 98) }, new { lead = M(8, 99), text = M(90, 100) } },
            callout = new { tone = "accent", text = M(220, 101) },
        }),
        (15, "numbered-cards", 4, "three numbered cards", false, 0, 3, null, _ => new
        {
            kicker = M(60, 102), headline = M(100, 103), reveal = "one-by-one",
            cards = new object[]
            {
                new { title = M(40, 104), text = M(140, 105) },
                new { title = M(40, 106), text = M(140, 107) },
                new { title = M(40, 108), text = M(140, 109), tone = "inverse", fill = "solid" },
            },
        }),
        (16, "bars", 4, "two bars and a marker", false, 0, 0, null, _ => new
        {
            kicker = M(60, 110), headline = M(100, 111),
            bars = new object[]
            {
                new { label = M(30, 112), sub = M(40, 113), value = 20, note = M(50, 114) },
                new { label = M(30, 115), sub = M(40, 116), value = 85, was = 40, note = M(50, 117), tone = "negative", tag = M(16, 118) },
            },
            wasLabel = M(24, 119), caption = M(160, 120),
        }),
        (17, "divider", 5, "part divider", false, 0, 0, null, _ => new { title = M(44, 121), sub = M(110, 122) }),
        (18, "flow", 5, "node, box of four, node", false, 0, 0, null, _ => new
        {
            kicker = M(60, 123), headline = M(100, 124),
            nodes = new object[]
            {
                new { label = M(20, 125), title = M(30, 126), text = M(120, 127) },
                new { title = M(30, 128), items = new[] { M(28, 129), M(28, 130), M(28, 131), M(28, 132) } },
                new { label = M(20, 133), title = M(30, 134), text = M(120, 135), tone = "inverse" },
            },
            caption = M(140, 136), chips = new[] { M(24, 137) },
        }),
        (19, "compare", 5, "two panels, one dark", false, 0, 0, null, _ => new
        {
            kicker = M(60, 138), headline = M(100, 139),
            failure = new
            {
                label = M(24, 140), title = M(50, 141), sub = M(100, 142), icon = "bulb", tone = "inverse", fill = "solid",
                points = new[] { M(90, 143) }, footer = M(90, 144),
            },
            success = new
            {
                label = M(24, 145), title = M(50, 146), sub = M(100, 147), icon = "eye", tone = "accent", fill = "soft",
                points = new[] { M(90, 148) }, footer = M(90, 149),
            },
        }),
        (20, "quote", 5, "dark quote with a figure", false, 0, 0, "dark", _ => new
        {
            kicker = M(60, 150), headline = M(80, 151), quote = M(280, 152), attribution = M(80, 153), role = M(160, 154),
            stat = new { value = M(12, 155), caption = M(90, 156) }, source = M(200, 158),
        }),
        (21, "image", 5, "screenshot and four tips", true, 0, 0, null, image => new
        {
            kicker = M(60, 159), headline = M(100, 160), image = new { image, alt = M(500, 161) },
            points = new object[]
            {
                new { icon = "check", lead = M(30, 162), text = M(80, 163) },
                new { icon = "search", lead = M(30, 164), text = M(80, 165) },
                new { icon = "people", lead = M(30, 166), text = M(80, 167) },
                new { icon = "wrench", lead = M(30, 168), text = M(80, 169) },
            },
            caption = M(140, 170),
        }),
        (22, "stack", 5, "three tiers and a caption", false, 0, 0, null, _ => new
        {
            kicker = M(60, 171), headline = M(100, 172),
            segments = new object[]
            {
                new { label = M(40, 173), note = M(40, 174), tone = "accent" },
                new { label = M(40, 175), note = M(40, 176) },
                new { label = M(40, 177), note = M(40, 178), tone = "inverse" },
            },
            caption = M(60, 179),
        }),
    ];

    private static string M(int length, int salt) => MadeUpWords.Of(length, salt);
}
