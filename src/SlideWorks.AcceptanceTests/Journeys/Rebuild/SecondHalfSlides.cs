namespace SlideWorks.AcceptanceTests.Journeys.Rebuild;

// Slides 23–44 of the 44-slide test deck, rebuilt as data: made-up Northwind text at every slot's documented maximum, every list at
// the count seen on the compare sheet. Slides 24 and 34 are custom layouts (see SW-489) and are not here. Number is the slide's
// number in the original deck; Part is the 1-based part to add it to; Callouts and Steps are what the original had and what the
// layout reports; Surface is the add_slide surface (dark where the original is dark); Photo is true when the original is a photo or
// a screenshot, so an img is expected (none in this half). A text's salt is the slide's number times 100 plus its place on the slide.
public static class SecondHalfSlides
{
    public static readonly IReadOnlyList<(int Number, string Layout, int? Part, string Kind, bool Photo, int Callouts, int Steps, string? Surface, Func<string, object> Slots)> ALL =
    [
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
        (26, "statement", 6, "big statement, boxed line", false, 0, 0, null, _ => new
        {
            kicker = M(60, 2601), statement = M(70, 2602), sub = M(100, 2603),
        }),
        (27, "bullets", 6, "dark list, red callout", false, 1, 0, "dark", _ => new
        {
            kicker = M(60, 2701), headline = M(100, 2702),
            items = new object[] { M(200, 2703) },
            callout = new { tone = "negative", text = M(220, 2704) },
        }),
        (28, "statement", 6, "big dark statement, boxed line", false, 0, 0, "dark", _ => new
        {
            statement = M(70, 2801), sub = M(100, 2802),
        }),
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
        (35, "quote", 7, "light quote with source", false, 0, 0, null, _ => new
        {
            kicker = M(60, 3501), quote = M(280, 3502), attribution = M(80, 3503), role = M(160, 3504), source = M(200, 3505),
        }),
        (36, "big-number", 7, "one big word, three ticks", false, 0, 0, null, _ => new
        {
            kicker = M(60, 3601), headline = M(100, 3602), value = "No.", caption = M(120, 3603), detail = M(200, 3604), source = M(100, 3605),
        }),
        (37, "table", 7, "two columns, struck rows", false, 0, 4, null, _ => new
        {
            kicker = M(60, 3701), headline = M(80, 3702), columns = new[] { M(30, 3703), M(30, 3704) }, reveal = "strike-and-answer",
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
        (39, "close", 7, "closing slide", false, 0, 0, null, _ => new
        {
            headline = M(36, 3901) + "\n" + M(36, 3902), sub = M(110, 3903), author = M(60, 3904),
        }),
        (40, "links", 7, "four links and a code", false, 0, 0, null, _ => new
        {
            kicker = M(60, 4001), headline = M(100, 4002),
            links = new object[]
            {
                new { kind = M(16, 4003), title = M(90, 4004), source = M(60, 4005), url = "https://northwind.example/talk" },
                new { kind = M(16, 4006), title = M(90, 4007), source = M(60, 4008), url = "https://northwind.example/podcast" },
                new { kind = M(16, 4009), title = M(90, 4010), source = M(60, 4011), url = "https://northwind.example/article" },
                new { kind = M(16, 4012), title = M(90, 4013), source = M(60, 4014), url = "https://northwind.example/testimony" },
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
        (44, "two-column", 8, "boxed text and evidence", false, 0, 0, null, _ => new
        {
            kicker = M(60, 4401), headline = M(100, 4402),
            left = new { title = M(60, 4403), text = M(400, 4404) },
            right = new { title = M(60, 4405), text = M(400, 4406) },
        }),
    ];

    private static string M(int length, int salt) => MadeUpWords.Of(length, salt);
}
