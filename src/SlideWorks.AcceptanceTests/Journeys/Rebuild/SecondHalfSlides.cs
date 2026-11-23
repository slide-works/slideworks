namespace SlideWorks.AcceptanceTests.Journeys.Rebuild;

// Slides 23–44 of the 44-slide test deck, rebuilt as data: made-up Northwind text, at every slot's documented maximum except the slots marked "shortened" below (each names its row in
// the SW-50 attachment "SW-50 rebuild check, slides 23–44.md"), every list at the count seen on the compare sheet. Slides 24 and 34 are custom layouts (see SW-489) and are not here. Number is the slide's
// number in the original deck; Part is the 1-based part to add it to; Callouts is the rebuild's count (what the layout draws) and Steps what the layout reports; Surface is the add_slide surface (dark where the original is dark); Photo is true when the original is a photo or
// a screenshot, so an img is expected (none in this half). A text's salt is the slide's number times 100 plus its place on the slide.
public static class SecondHalfSlides
{
    public static readonly IReadOnlyList<(int Number, string Layout, int? Part, string Kind, bool Photo, int Callouts, int Steps, string? Surface, Func<string, object> Slots)> ALL =
    [
        // shortened: no badges, card title 40 and text 90 (row 23)
        (23, "cards", 5, "three cards, callout", false, 1, 0, null, _ => new
        {
            kicker = M(60, 2301), headline = M(100, 2302),
            cards = new object[]
            {
                new { icon = "check", title = M(40, 2304), text = M(90, 2305), tone = "accent", fill = "plain" },
                new { icon = "cycle", title = M(40, 2307), text = M(90, 2308), tone = "accent", fill = "plain" },
                new { icon = "eye", title = M(40, 2310), text = M(90, 2311), tone = "inverse", fill = "solid" },
            },
            callout = new { tone = "accent", text = M(220, 2312) },
        }),
        (25, "divider", 6, "part divider", false, 0, 0, null, _ => new { title = M(44, 2501), sub = M(110, 2502) }),
        // shortened: statement 69, not 70 (SW-802 estimate)
        (26, "statement", 6, "big statement, sub line", false, 0, 0, null, _ => new
        {
            kicker = M(60, 2601), statement = M(69, 2602), sub = M(100, 2603),
            emphasis = new[] { new { text = Last(M(69, 2602)), tone = "accent", @in = "statement" } },
        }),
        (27, "bullets", 6, "dark list, red callout", false, 1, 0, "dark", _ => new
        {
            kicker = M(60, 2701), headline = M(100, 2702),
            items = new object[] { M(200, 2703) },
            callout = new { tone = "negative", text = M(220, 2704) },
        }),
        // shortened: statement 69, not 70 (SW-802 estimate)
        (28, "statement", 6, "big dark statement, sub line", false, 0, 0, "dark", _ => new
        {
            statement = M(69, 2801), sub = M(100, 2802),
            emphasis = new[] { new { text = Tail(M(100, 2802), 60), tone = "accent", @in = "sub" } }, // the original emphasises the whole sub line; the slot takes 60 characters
        }),
        // shortened: no stamps, footers 60 (row 29)
        (29, "compare", 6, "two panels, rows, stamps", false, 0, 0, null, _ => new
        {
            kicker = M(60, 2901), headline = M(100, 2902),
            failure = new
            {
                label = M(24, 2903), title = M(50, 2904), icon = "factory", tone = "negative", fill = "soft",
                rows = new object[] { new { label = M(16, 2905), value = M(40, 2906) }, new { label = M(16, 2907), value = M(40, 2908) } },
                footer = M(60, 2910),
            },
            success = new
            {
                label = M(24, 2911), title = M(50, 2912), icon = "people", tone = "positive", fill = "soft",
                rows = new object[] { new { label = M(16, 2913), value = M(40, 2914) }, new { label = M(16, 2915), value = M(40, 2916) } },
                footer = M(60, 2918),
            },
        }),
        (30, "compare", 6, "two panels, quoted lines", false, 0, 0, null, _ => new
        {
            kicker = M(60, 3001), headline = M(100, 3002),
            failure = new { label = M(24, 3003), title = M(50, 3004), line = M(100, 3005), tone = "inverse", fill = "solid", footer = M(90, 3006) },
            success = new { label = M(24, 3007), title = M(50, 3008), line = M(100, 3009), tone = "accent", fill = "solid", footer = M(90, 3010) },
        }),
        (31, "quote", 6, "dark pull quote", false, 0, 0, "dark", _ => new
        {
            kicker = M(60, 3101), quote = M(280, 3102), attribution = M(80, 3103), role = M(160, 3104),
            emphasis = new[] { new { text = Tail(M(280, 3102), 60), tone = "accent", @in = "quote" } }, // the original emphasises the second half of the quote; the slot takes 60 characters
        }),
        (32, "divider", 7, "part divider", false, 0, 0, null, _ => new { title = M(44, 3201), sub = M(110, 3202) }),
        (33, "numbered-cards", 7, "three numbered cards, callout", false, 1, 0, null, _ => new
        {
            kicker = M(60, 3301), headline = M(100, 3302),
            cards = new object[]
            {
                new { title = M(40, 3303), text = M(140, 3304) },
                new { title = M(40, 3305), text = M(140, 3306) },
                new { title = M(40, 3307), text = M(140, 3308), tone = "inverse", fill = "solid" },
            },
            callout = new { tone = "accent", text = M(220, 3309) },
        }),
        (35, "quote", 7, "dark quote with source", false, 0, 0, "dark", _ => new
        {
            kicker = M(60, 3501), quote = M(280, 3502), attribution = M(80, 3503), role = M(160, 3504), source = M(200, 3505),
            emphasis = new[] { new { text = Head(M(280, 3502), 60), tone = "negative", @in = "quote" } },
        }),
        // value is a number with a unit where the original has a word (row 36)
        (36, "big-number", 7, "one large figure, three ticks", false, 0, 0, null, _ => new
        {
            kicker = M(60, 3601), headline = M(100, 3602), value = "360606 wks",
            emphasis = new[] { new { text = Last(M(100, 3602)), tone = "accent", @in = "headline" } }, caption = M(120, 3603), detail = M(200, 3604), source = M(100, 3605),
        }),
        // shortened: cells 80, not 120 (row 37)
        (37, "table", 7, "two columns, struck rows", false, 0, 4, null, _ => new
        {
            kicker = M(60, 3701), headline = M(100, 3702), columns = new[] { M(30, 3703), M(30, 3704) }, reveal = "strike-and-answer",
            rows = new object[]
            {
                new[] { M(80, 3705), M(80, 3706) },
                new[] { M(80, 3707), M(80, 3708) },
                new[] { M(80, 3709), M(80, 3710) },
                new[] { M(80, 3711), M(80, 3712) },
            },
        }),
        (38, "cards", 7, "three cards with icons", false, 0, 0, null, _ => new
        {
            kicker = M(60, 3801), headline = M(100, 3802),
            cards = new object[]
            {
                new { icon = "coins", badge = M(36, 3803), title = M(70, 3804), text = M(140, 3805), tone = "accent", fill = "plain" },
                new { icon = "cycle", badge = M(36, 3806), title = M(70, 3807), text = M(140, 3808), tone = "positive", fill = "plain" },
                new { icon = "warning", badge = M(36, 3809), title = M(70, 3810), text = M(140, 3811), tone = "negative", fill = "plain" },
            },
        }),
        // shortened: second headline line 30, not 40 (SW-802 estimate)
        (39, "close", 7, "closing slide", false, 0, 0, null, _ => new
        {
            headline = M(39, 3901) + "\n" + M(30, 3902), sub = M(110, 3903), author = M(60, 3904),
        }),
        // shortened: link sources 60 (row 40)
        (40, "links", 7, "four links and a code", false, 0, 0, null, _ => new
        {
            kicker = M(60, 4001), headline = M(100, 4002),
            links = new object[]
            {
                new { kind = M(16, 4003), title = M(90, 4004), source = M(60, 4005), url = "https://northwind.example/a" },
                new { kind = M(16, 4006), title = M(90, 4007), source = M(60, 4008), url = "https://northwind.example/b" },
                new { kind = M(16, 4009), title = M(90, 4010), source = M(60, 4011), url = "https://northwind.example/c" },
                new { kind = M(16, 4012), title = M(90, 4013), source = M(60, 4014), url = "https://northwind.example/d" },
            },
            qr = new { url = "https://northwind.example/more", caption = M(60, 4015) },
        }),
        (41, "divider", 8, "part divider, marked part", false, 0, 0, null, _ => new { title = M(44, 4101), sub = M(110, 4102) }),
        (42, "flow", 8, "three cards, arrows, pills", false, 0, 0, null, _ => new
        {
            kicker = M(60, 4201), headline = M(100, 4202),
            nodes = new object[]
            {
                new { title = M(30, 4203), text = M(120, 4204) },
                new { title = M(30, 4205), text = M(120, 4206) },
                new { title = M(30, 4207), text = M(120, 4208) },
            },
            caption = M(140, 4209), chips = new[] { M(24, 4210), M(24, 4211) },
        }),
        (43, "compare", 8, "two panels, one big word each", false, 0, 0, null, _ => new
        {
            kicker = M(60, 4301), headline = M(100, 4302),
            failure = new { label = M(24, 4303), title = M(50, 4304), tone = "positive", fill = "soft", points = new[] { M(90, 4305) }, footer = M(90, 4306) },
            success = new { label = M(24, 4307), title = M(50, 4308), tone = "negative", fill = "soft", points = new[] { M(90, 4309) }, footer = M(90, 4310) },
        }),
        // shortened: right text 300, no left title (row 44)
        (44, "two-column", 8, "boxed text, headed text", false, 1, 0, null, _ => new
        {
            kicker = M(60, 4401), headline = M(100, 4402),
            left = new { text = M(400, 4404) },
            right = new { title = M(60, 4405), text = M(300, 4406) },
            callout = new { tone = "accent", text = M(220, 4407) },
        }),
    ];

    private static string M(int length, int salt) => MadeUpWords.Of(length, salt);

    // The last word of a text, and the last {max} characters or fewer starting at a word: phrases that occur once in their text.
    private static string Last(string text) => text[(text.LastIndexOf(' ') + 1)..];

    // The first words, up to {max} characters: the capital letter makes the phrase occur once.
    private static string Head(string text, int max) => text[..text.LastIndexOf(' ', max)];

    private static string Tail(string text, int max)
    {
        // The made-up words repeat, so the window shrinks until the phrase occurs once.
        for (var size = max; size > 0; size -= 4)
        {
            var from = Math.Max(0, text.Length - size);
            if (from > 0)
            {
                var space = text.IndexOf(' ', from);
                if (space < 0)
                {
                    break;
                }
                from = space + 1;
            }
            var phrase = text[from..];
            if (text.IndexOf(phrase, StringComparison.Ordinal) == text.LastIndexOf(phrase, StringComparison.Ordinal))
            {
                return phrase;
            }
        }
        return Last(text);
    }
}
